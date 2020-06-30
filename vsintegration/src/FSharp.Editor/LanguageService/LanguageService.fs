// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace rec Microsoft.VisualStudio.FSharp.Editor

open System
open Microsoft.CodeAnalysis.Options
open FSharp.Compiler.SourceCodeServices
open Microsoft.VisualStudio
open Microsoft.VisualStudio.FSharp.Editor

#nowarn "9" // NativePtr.toNativeInt

// Used to expose FSharpChecker/ProjectInfo manager to diagnostic providers
// Diagnostic providers can be executed in environment that does not use MEF so they can rely only
// on services exposed by the workspace
type internal FSharpCheckerWorkspaceService =
    inherit Microsoft.CodeAnalysis.Host.IWorkspaceService
    abstract Checker: FSharpChecker
    abstract FSharpProjectOptionsManager: FSharpProjectOptionsManager

type internal RoamingProfileStorageLocation(keyName: string) =
    inherit OptionStorageLocation()
    
    member __.GetKeyNameForLanguage(languageName: string) =
        let unsubstitutedKeyName = keyName
        match languageName with
        | null -> unsubstitutedKeyName
        | _ ->
            let substituteLanguageName = if languageName = FSharpConstants.FSharpLanguageName then "FSharp" else languageName
            unsubstitutedKeyName.Replace("%LANGUAGE%", substituteLanguageName)
 
[<Composition.Shared>]
[<Microsoft.CodeAnalysis.Host.Mef.ExportWorkspaceServiceFactory(typeof<FSharpCheckerWorkspaceService>, Microsoft.CodeAnalysis.Host.Mef.ServiceLayer.Default)>]
type internal FSharpCheckerWorkspaceServiceFactory
    [<Composition.ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider,
        projectInfoManager: FSharpProjectOptionsManager
    ) =
    interface Microsoft.CodeAnalysis.Host.Mef.IWorkspaceServiceFactory with
        member __.CreateService(_workspaceServices) =
            upcast { new FSharpCheckerWorkspaceService with
                member __.Checker = checkerProvider.Checker
                member __.FSharpProjectOptionsManager = projectInfoManager }

[<Microsoft.CodeAnalysis.Host.Mef.ExportWorkspaceServiceFactory(typeof<EditorOptions>, Microsoft.CodeAnalysis.Host.Mef.ServiceLayer.Default)>]
type internal FSharpSettingsFactory
    [<Composition.ImportingConstructor>] (settings: EditorOptions) =
    interface Microsoft.CodeAnalysis.Host.Mef.IWorkspaceServiceFactory with
        member __.CreateService(_) = upcast settings

