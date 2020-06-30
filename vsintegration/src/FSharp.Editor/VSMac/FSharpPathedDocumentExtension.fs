namespace MonoDevelop.FSharp
open System
open System.Diagnostics
open System.Linq
open System.Threading

open FSharp.Compiler.SourceCodeServices

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Threading

open MonoDevelop.Components
open MonoDevelop.Core
open MonoDevelop.Ide
open MonoDevelop.Ide.Gui.Content
open MonoDevelop.Projects

module VSMacIcons =
    /// Translates icon code that we get from F# language service into a MonoDevelop icon
    let getIcon (navItem: FSharpNavigationDeclarationItem) =
        match navItem.Kind with
        | NamespaceDecl -> "md-name-space"
        | _ ->
            match navItem.Glyph with
            | FSharpGlyph.Class -> "md-class"
            | FSharpGlyph.Enum -> "md-enum"
            | FSharpGlyph.Struct -> "md-struct"
            | FSharpGlyph.ExtensionMethod -> "md-struct"
            | FSharpGlyph.Delegate -> "md-delegate"
            | FSharpGlyph.Interface -> "md-interface"
            | FSharpGlyph.Module -> "md-module"
            | FSharpGlyph.NameSpace -> "md-name-space"
            | FSharpGlyph.Method -> "md-method"
            | FSharpGlyph.OverridenMethod -> "md-method"
            | FSharpGlyph.Property -> "md-property"
            | FSharpGlyph.Event -> "md-event"
            | FSharpGlyph.Constant -> "md-field"
            | FSharpGlyph.EnumMember -> "md-field"
            | FSharpGlyph.Exception -> "md-exception"
            | FSharpGlyph.Typedef -> "md-class"
            | FSharpGlyph.Type -> "md-type"
            | FSharpGlyph.Union -> "md-type"
            | FSharpGlyph.Variable -> "md-field"
            | FSharpGlyph.Field -> "md-field"
            | FSharpGlyph.Error -> "md-breakpint"

type internal FSharpPathedDocumentExtension(projectInfoManager: FSharpProjectOptionsManager, checker: FSharpChecker, view: ITextView, joinableTaskContext: JoinableTaskContext) as x =

    let pathChanged = new Event<_,_>()
    let mutable currentPath = [||]
    let mutable subscriptions = ResizeArray<IDisposable>()
    let mutable ownerProjects = ResizeArray<DotNetProject>()
    let mutable lastOwnerProjects = ResizeArray<DotNetProject>()
    let mutable registration: WorkspaceRegistration = null

    let textContainer = view.TextBuffer.AsTextContainer()

    let getRelatedDocuments(container: SourceTextContainer) =
        let workspace = IdeApp.TypeSystemService.Workspace
        let sol = workspace.CurrentSolution
        let ids = workspace.GetRelatedDocumentIds(container)

        ids
        |> Seq.map(fun id -> sol.GetDocument(id))
        |> Seq.filter (isNull >> not)

    let getActiveDocument() =
        let workspace = IdeApp.TypeSystemService.Workspace
        let id = workspace.GetDocumentIdInCurrentContext(textContainer)
        workspace.CurrentSolution.GetDocument(id) |> Option.ofObj

    let workspaceChanged(_) =
        ownerProjects.Clear()
        match getActiveDocument() with
        | Some activeDocument ->
            let activeProj = IdeServices.TypeSystemService.GetMonoProject(activeDocument.Project) :?> DotNetProject
            if activeProj <> null then
                ownerProjects.Add activeProj
            else
                for document in textContainer |> getRelatedDocuments do
                let dotnetProj = IdeServices.TypeSystemService.GetMonoProject(document.Project) :?> DotNetProject
                if dotnetProj <> null && document.Project.Id <> activeDocument.Project.Id then
                    ownerProjects.Add dotnetProj

        | None -> ()

    let getOpenDocumentInCurrentContextWithChanges(text: SourceText) =
        let workspace = IdeApp.TypeSystemService.Workspace
        let solution = workspace.CurrentSolution
        let id = workspace.GetDocumentIdInCurrentContext(text.Container)
        if id = null || not(solution.ContainsDocument(id)) then
            null
        else
            // We update all linked files to ensure they are all in sync. Otherwise code might try to jump from
            // one linked file to another and be surprised if the text is entirely different.
            let allIds = workspace.GetRelatedDocumentIds(text.Container)
            solution.WithDocumentText(allIds, text, PreservationMode.PreserveIdentity)
                    .GetDocument(id)

    let mutable lastSnapshot: ITextSnapshot = null
    let mutable lastOffset = 0

    let update(position: CaretPosition) =
        let snapshot = position.BufferPosition.Snapshot
        let offset = position.BufferPosition.Position

        if lastSnapshot = snapshot && lastOffset = offset && ownerProjects.SequenceEqual(lastOwnerProjects) then
            ()
        else
            lastSnapshot <- snapshot
            lastOffset <- offset
            lastOwnerProjects <- ownerProjects.ToList()

            let roslynDocument = snapshot.AsText() |> getOpenDocumentInCurrentContextWithChanges
            if roslynDocument <> null then
                x.Update(roslynDocument, position.BufferPosition.Position)


    let caretPositionChanged (caretPositionChangedArgs: CaretPositionChangedEventArgs) =
        update caretPositionChangedArgs.NewPosition
        
    let textBufferChanged(_args) =
        update view.Caret.Position

    do
        registration <- Microsoft.CodeAnalysis.Workspace.GetWorkspaceRegistration(textContainer)
        subscriptions.Add (registration.WorkspaceChanged.Subscribe(workspaceChanged))
        subscriptions.Add (view.TextBuffer.PostChanged.Subscribe(textBufferChanged))
        subscriptions.Add (view.Caret.PositionChanged.Subscribe(caretPositionChanged))
        currentPath <- [| new PathEntry(GettextCatalog.GetString("No selection")) |]
        workspaceChanged None

    member x.GetEntityMarkup(node: FSharpNavigationDeclarationItem) =
        let name = node.Name.Split('.')
        if name.Length > 0 then name.Last()
        else node.Name

    member x.GetNavigationItems(document:Document, fsSourceText) =
        asyncMaybe {
            let! parsingOptions, _options = projectInfoManager.TryGetOptionsByProject(document.Project, Async.DefaultCancellationToken) 

            let! parseResults = checker.ParseFile(document.FilePath, fsSourceText, parsingOptions) |> liftAsync

            try 
                return parseResults.GetNavigationItems().Declarations
            with _ ->
                Debug.Assert(false, "couldn't update navigation items, ignoring")
                return [| |]
        }

    member val SourceText : SourceText = null with get, set

    member private x.Update(document:Document, caretOffset) =
        let caretLocation = TextSpan(caretOffset, 1)

        asyncMaybe {
            let! sourceText = document.GetTextAsync(Async.DefaultCancellationToken) |> liftTaskAsync
            x.SourceText <- sourceText
            let fsSourceText = sourceText.ToFSharpSourceText()
            let! toplevel = x.GetNavigationItems(document, fsSourceText)

            let topLevelTypesInsideCursor =
                toplevel
                |> Array.filter (fun tl -> let range = tl.Declaration.Range
                                           let declLocation = RoslynHelpers.FSharpRangeToTextSpan(sourceText, range)
                                           caretLocation.IntersectsWith(declLocation))
                |> Array.sortBy(fun xs -> xs.Declaration.Range.StartLine)
      
            let newPath = ResizeArray<_>()

            let paths =
                [ for top in topLevelTypesInsideCursor do
                    let name = top.Declaration.Name
                    let navitems =
                        if name.Contains(".") then
                            let nameparts = name.[.. name.LastIndexOf(".")]
                            toplevel |> Array.filter (fun decl -> decl.Declaration.Name.StartsWith(nameparts))
                        else toplevel
                    yield (Some top.Declaration, (upcast navitems : obj))
          
                  if topLevelTypesInsideCursor.Length > 0 then
                    let lastToplevel = topLevelTypesInsideCursor.Last()
                    //only first child found is returned, could there be multiple children found?
                    let child =
                        lastToplevel.Nested
                        |> Array.tryFind (fun tl -> let range = RoslynHelpers.FSharpRangeToTextSpan(sourceText, tl.Range)
                                                    caretLocation.IntersectsWith(range))
                    match child with
                    | Some c  -> yield (Some c, upcast lastToplevel)
                    | None -> yield (None, upcast lastToplevel) ]       
            let previousPath = currentPath

            Runtime.RunInMainThread(fun() ->
                paths |> List.iter(fun (declItemOption, tag) ->
                    match declItemOption with
                    | Some declItem ->
                        newPath.Add(new PathEntry(icon = ImageService.GetIcon(VSMacIcons.getIcon declItem, Gtk.IconSize.Menu),
                                                  markup =x.GetEntityMarkup(declItem),
                                                  Tag = tag))
                    | None ->
                        newPath.Add(new PathEntry("No selection", Tag = tag)))
                    
                let samePath = Seq.forall2 (fun (p1:PathEntry) (p2:PathEntry) -> p1.Markup = p2.Markup) previousPath newPath
                    
                //ensure the path has changed from the previous one before setting and raising event.
                if not samePath then
                    if newPath.Count = 0 then currentPath <- [|new PathEntry("No selection", Tag = null)|]
                    else currentPath <- newPath.ToArray()
          
                    //invoke pathChanged
                    pathChanged.Trigger(x, DocumentPathChangedEventArgs(previousPath))) |> ignore
        } 
        |> Async.Ignore
        |> Async.Start

    interface IDisposable with
        override x.Dispose() =
            for disposable in subscriptions do disposable.Dispose()
            subscriptions.Clear()

    interface IPathedDocument with
        member x.CurrentPath = currentPath

        member x.CreatePathWidget(index) =
            let path = (x :> IPathedDocument).CurrentPath
            if path = null || index < 0 || index >= path.Length then null else
            let tag = path.[index].Tag
            let window = new DropDownBoxListWindow(FSharpDataProvider(x, tag, view), FixedRowHeight=22, MaxVisibleRows=14)
            window.SelectItem (path.[index].Tag)
            MonoDevelop.Components.Control.op_Implicit window

        member x.add_PathChanged(handler) = pathChanged.Publish.AddHandler(handler)
        member x.remove_PathChanged(handler) = pathChanged.Publish.RemoveHandler(handler)

and internal FSharpDataProvider(ext:FSharpPathedDocumentExtension, tag, view: ITextView) =
    let memberList = ResizeArray<_>()

    let reset() =
        memberList.Clear()
        match tag with
        | :? array<FSharpNavigationTopLevelDeclaration> as navitems ->
            for decl in navitems do
                memberList.Add(decl.Declaration)
        | :? FSharpNavigationTopLevelDeclaration as tld ->
            memberList.AddRange(tld.Nested)
        | _ -> ()

    do reset()

    interface DropDownBoxListWindow.IListDataProvider with
        member x.IconCount =
            memberList.Count

        member x.Reset() = reset()

        member x.GetTag (n) =
            memberList.[n] :> obj

        member x.ActivateItem(n) =
            let node = memberList.[n]
            let pos = RoslynHelpers.FSharpRangeToTextSpan(ext.SourceText, node.Range).Start
            let point = new SnapshotPoint(view.TextSnapshot, pos)
            view.Caret.MoveTo(point) |> ignore
            view.ViewScroller.EnsureSpanVisible(new SnapshotSpan(view.Caret.Position.BufferPosition, 0), EnsureSpanVisibleOptions.AlwaysCenter);

        member x.GetMarkup(n) =
            let node = memberList.[n]
            ext.GetEntityMarkup (node)

        member x.GetIcon(n) =
            let node = memberList.[n]
            ImageService.GetIcon(VSMacIcons.getIcon node, Gtk.IconSize.Menu)

