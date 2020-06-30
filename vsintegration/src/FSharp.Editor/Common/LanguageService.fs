namespace MonoDevelop.FSharp
open System
open System.Collections.Generic
open System.IO
open FSharp.Compiler
open FSharp.Compiler.SourceCodeServices
open MonoDevelop.FSharp
open MonoDevelop.Core
open MonoDevelop.Ide
open MonoDevelop.Projects
open Microsoft.VisualStudio.FSharp.Editor.Extensions

module Symbol =
    /// We always know the text of the identifier that resolved to symbol.
    /// Trim the range of the referring text to only include this identifier.
    /// This means references like A.B.C are trimmed to "C".  This allows renaming to just rename "C".
    let trimSymbolRegion(symbolUse:FSharpSymbolUse) (lastIdentAtLoc:string) =
        let m = symbolUse.RangeAlternate
        let ((beginLine, beginCol), (endLine, endCol)) = ((m.StartLine, m.StartColumn), (m.EndLine, m.EndColumn))
    
        let (beginLine, beginCol) =
            if endCol >=lastIdentAtLoc.Length && (beginLine <> endLine || (endCol-beginCol) >= lastIdentAtLoc.Length) then
                (endLine,endCol-lastIdentAtLoc.Length)
            else
                (beginLine, beginCol)
        Range.mkPos beginLine beginCol, Range.mkPos endLine endCol

/// Contains settings of the F# language service
module ServiceSettings =
    let internal getEnvInteger e dflt = match System.Environment.GetEnvironmentVariable(e) with null -> dflt | t -> try int t with _ -> dflt
    /// When making blocking calls from the GUI, we specify this value as the timeout, so that the GUI is not blocked forever
    let blockingTimeout = getEnvInteger "FSharpBinding_BlockingTimeout" 1000
    let maximumTimeout = getEnvInteger "FSharpBinding_MaxTimeout" 10000
    let idleBackgroundCheckTime = getEnvInteger "FSharpBinding_IdleBackgroundCheckTime" 2000
 
[<RequireQualifiedAccess>]
type AllowStaleResults =
    // Allow checker results where the source doesn't even match
    | MatchingFileName
    // Allow checker results where the source matches but where the background builder may not have caught up yet after some other change
    | MatchingSource

//type Debug = System.Console
open Microsoft.VisualStudio.FSharp.Editor.Pervasive

/// Provides functionality for working with the F# interactive checker running in background
type LanguageService(checker: FSharpChecker, dirtyNotify, _extraProjectInfo) as x =

    /// Load times used to reset type checking properly on script/project load/unload. It just has to be unique for each project load/reload.
    /// Not yet sure if this works for scripts.
    let fakeDateTimeRepresentingTimeLoaded proj = DateTime(abs (int64 (match proj with null -> 0 | _ -> proj.GetHashCode())) % 103231L)
    let checkProjectResultsCache = Collections.Generic.Dictionary<string, _>()


    let loadingProjects = HashSet<string>()

    let showStatusIcon projectFileName =
        if loadingProjects.Add projectFileName then
            IdeApp.TypeSystemService.BeginWorkspaceLoad()

    let hideStatusIcon projectFileName =
        if loadingProjects.Remove projectFileName then
            IdeApp.TypeSystemService.EndWorkspaceLoad()

    /// When creating new script file on Mac, the filename we get sometimes
    /// has a name //foo.fsx, and as a result 'Path.GetFullPath' throws in the F#
    /// language service - this fixes the issue by inventing nicer file name.
    let fixFileName path =
        if (try Path.GetFullPath(path) |> ignore; true
              with _ -> false) then path
        else
            let dir =
                if Environment.OSVersion.Platform = PlatformID.Unix ||
                    Environment.OSVersion.Platform = PlatformID.MacOSX then
                    Environment.GetEnvironmentVariable("HOME")
                else
                    Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%")
            Path.Combine(dir, Path.GetFileName(path))

    let optionsForDependentProject projectFile =
        let project = x.GetProjectFromFileName projectFile
        async {
            let! assemblies = async {
                match project with
                | Some (proj:DotNetProject) -> return! proj.GetReferences(CompilerArguments.getConfig()) |> Async.AwaitTask
                | None -> return new List<AssemblyReference> ()
            }
            return x.GetProjectCheckerOptions(projectFile, [], assemblies)
        }

    member x.HideStatusIcon = hideStatusIcon

    member x.GetProjectFromFileName projectFile =
        IdeApp.Workspace.GetAllProjects()
        |> Seq.tryFind (fun p -> p.FileName.FullPath.ToString() = projectFile)
        |> Option.map(fun p -> p :?> DotNetProject)

    member x.GetProjectOptionsFromProjectFile (project:DotNetProject) (config:ConfigurationSelector) (referencedAssemblies:AssemblyReference seq) =

        // hack: we can't just pull the refs out of referencedAssemblies as we use this for referenced projects as well
        let getReferencedFSharpProjects (project:DotNetProject) =
            project.GetReferencedAssemblyProjects config
            |> Seq.filter (fun p -> p <> project && p.SupportedLanguages |> Array.contains "F#")

        let rec getOptions referencedProject =
            // hack: we use the referencedAssemblies of the root project for the dependencies' options as well
            // which is obviously wrong, but it doesn't seem to matter in this case
            let projectOptions = CompilerArguments.getArgumentsFromProject referencedProject config referencedAssemblies
            //match projectOptions with
            //| Some projOptions ->
            let referencedProjectOptions =
                referencedProject
                |> getReferencedFSharpProjects
                |> Seq.fold (fun acc reference ->
                                 match getOptions reference with
                                 | Some outFile, Some opts  -> (outFile, opts) :: acc
                                 | _ -> acc) ([])

            (Some (referencedProject.GetOutputFileName(config).ToString()), Some ({ projectOptions with ReferencedProjects = referencedProjectOptions |> Array.ofList } ))
            //| None -> None, None
        let _file, projectOptions = getOptions project
        projectOptions

    /// Constructs options for the interactive checker for a project under the given configuration.
    member x.GetProjectCheckerOptions(projFilename, ?properties, ?referencedAssemblies) : FSharpProjectOptions option =
        let config =
            maybe {
                let! ws = IdeApp.Workspace |> Option.ofObj
                return! ws.ActiveConfiguration |> Option.ofObj
            } |> Option.defaultValue ConfigurationSelector.Default
        let configId =
            match IdeApp.Workspace with
            | null -> null
            | ws -> ws.ActiveConfigurationId
        let properties = defaultArg properties ["Configuration", configId]
        showStatusIcon projFilename

        checker.ProjectChecked.Add (fun (filename, _) -> 
            hideStatusIcon filename)

        let project =
            IdeApp.Workspace.GetAllProjects()
            |> Seq.tryFind (fun p -> p.FileName.FullPath.ToString() = projFilename)

        match project with
        | Some proj ->
            let proj = proj :?> DotNetProject
            //fixme eliminate this .Result
            let asms = match referencedAssemblies with
                       | Some a -> a
                       | None -> (proj.GetReferences config).Result
            let opts = x.GetProjectOptionsFromProjectFile proj config asms
            opts |> Option.bind(fun opts' ->
                // Print contents of check option for debugging purposes
                LoggingService.logDebug "GetProjectCheckerOptions: ProjectFileName: %s, ProjectFileNames: %A, ProjectOptions: %A, IsIncompleteTypeCheckEnvironment: %A, UseScriptResolutionRules: %A"
                    opts'.ProjectFileName opts'.SourceFiles opts'.OtherOptions opts'.IsIncompleteTypeCheckEnvironment opts'.UseScriptResolutionRules
                opts)
        | None -> None
