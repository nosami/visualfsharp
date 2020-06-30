namespace MonoDevelop.FSharp

open System
open System.Diagnostics

open FSharp.Compiler.SourceCodeServices
open Gtk
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Threading

open MonoDevelop.Components
open MonoDevelop.Core
open MonoDevelop.DesignerSupport
open MonoDevelop.Ide
open MonoDevelop.Ide.Gui.Components

type internal FSharpOutlineDocumentExtension(projectInfoManager: FSharpProjectOptionsManager, checker: FSharpChecker, view: ITextView, joinableTaskContext: JoinableTaskContext) as x =
    let mutable treeView : PadTreeView option = None
    let mutable refreshingOutline : bool = false
    let mutable timerId : uint32 = 0u

    let mutable subscriptions = ResizeArray<IDisposable>()

    let textContainer = view.TextBuffer.AsTextContainer()

    let registration = Workspace.GetWorkspaceRegistration(textContainer)

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

    let getNavigationItems(document:Document, fsSourceText) =
        asyncMaybe {
            let! parsingOptions, _options = projectInfoManager.TryGetOptionsByProject(document.Project, Async.DefaultCancellationToken) 

            let! parseResults = checker.ParseFile(document.FilePath, fsSourceText, parsingOptions) |> liftAsync

            try 
                return parseResults.GetNavigationItems().Declarations
            with _ ->
                Debug.Assert(false, "couldn't update navigation items, ignoring")
                return [| |]
        }

    let refillTree() =
        match treeView with
        | Some(treeView) ->

            Runtime.AssertMainThread()
            refreshingOutline <- false

            if treeView.IsRealized then
                asyncMaybe {
                    let sourceText = view.TextBuffer.CurrentSnapshot.AsText()
                    let! document = getOpenDocumentInCurrentContextWithChanges sourceText |> Option.ofObj
                    let fsSourceText = sourceText.ToFSharpSourceText()
                    let! navItems = getNavigationItems(document, fsSourceText)
                    Runtime.RunInMainThread(fun() ->
                        let treeStore = treeView.Model :?> TreeStore
                        treeStore.Clear()
                        let toplevel = navItems
                                       |> Array.sortBy(fun xs -> xs.Declaration.Range.StartLine)

                        for item in toplevel do
                            let iter = treeStore.AppendValues([| item.Declaration |])
                            let children = item.Nested
                                           |> Array.sortBy(fun xs -> xs.Range.StartLine)

                            for nested in children do
                                treeStore.AppendValues(iter, [| nested |]) |> ignore

                        treeView.ExpandAll()
                        timerId <- 0u) |> ignore
                } 
                |> Async.Ignore
                |> Async.Start

                Gdk.Threads.Leave()
        | None -> ()

        refreshingOutline <- false
        false

    let updateDocumentOutline _ =
        if not refreshingOutline then
            refreshingOutline <- true
            timerId <- GLib.Timeout.Add (1000u, (fun _ -> refillTree()))

    do
        subscriptions.Add (registration.WorkspaceChanged.Subscribe(updateDocumentOutline))
        subscriptions.Add (view.TextBuffer.PostChanged.Subscribe(updateDocumentOutline))
        updateDocumentOutline None

    interface IDisposable with
        override x.Dispose() =
            if timerId > 0u then
                GLib.Source.Remove timerId |> ignore
            for disposable in subscriptions do disposable.Dispose()
            subscriptions.Clear()
            timerId <- 0u

    interface IOutlinedDocument with
        member x.GetOutlineWidget() =
            match treeView with
            | Some(treeView) -> treeView :> Widget
            | None ->
                let treeStore = new TreeStore(typedefof<obj>)
                let padTreeView = new PadTreeView(treeStore, HeadersVisible = true)

                let setCellIcon _column (cellRenderer : CellRenderer) (treeModel : TreeModel) (iter : TreeIter) =
                    let pixRenderer = cellRenderer :?> CellRendererImage
                    treeModel.GetValue(iter, 0)
                    |> Option.tryCast<FSharpNavigationDeclarationItem[]>
                    |> Option.iter(fun item ->
                        pixRenderer.Image <- ImageService.GetIcon(VSMacIcons.getIcon item.[0], Gtk.IconSize.Menu))

                let setCellText _column (cellRenderer : CellRenderer) (treeModel : TreeModel) (iter : TreeIter) =
                    let renderer = cellRenderer :?> CellRendererText
                    treeModel.GetValue(iter, 0)
                    |> Option.tryCast<FSharpNavigationDeclarationItem[]>
                    |> Option.iter(fun item -> renderer.Text <- item.[0].Name)

                let jumpToDeclaration focus =
                    let iter : TreeIter ref = ref Unchecked.defaultof<_>
                    if padTreeView.Selection.GetSelected(iter) then
                        padTreeView.Model.GetValue(!iter, 0)
                        |> Option.tryCast<FSharpNavigationDeclarationItem[]>
                        |> Option.iter(fun item ->
                            let sourceText = view.TextBuffer.CurrentSnapshot.AsText()
                            let node = item.[0]
                            let pos = RoslynHelpers.FSharpRangeToTextSpan(sourceText, node.Range).Start
                            let point = new SnapshotPoint(view.TextSnapshot, pos)
                            view.Caret.MoveTo(point) |> ignore
                            view.ViewScroller.EnsureSpanVisible(new SnapshotSpan(view.Caret.Position.BufferPosition, 0), EnsureSpanVisibleOptions.AlwaysCenter))

                    if focus then
                        view 
                        |> Option.tryCast<ICocoaTextView>
                        |> Option.iter(fun view -> view.Focus())

                treeView <- Some padTreeView

                let pixRenderer = new CellRendererImage(Xpad = 0u, Ypad = 0u)
                padTreeView.TextRenderer.Xpad <- 0u
                padTreeView.TextRenderer.Ypad <- 0u

                let treeCol = new TreeViewColumn()
                treeCol.PackStart(pixRenderer, false)
                treeCol.SetCellDataFunc(pixRenderer, new TreeCellDataFunc(setCellIcon))
                treeCol.PackStart(padTreeView.TextRenderer, true)
                treeCol.SetCellDataFunc(padTreeView.TextRenderer, new TreeCellDataFunc(setCellText))

                padTreeView.AppendColumn treeCol |> ignore
                subscriptions.Add(padTreeView.Realized.Subscribe(fun _ -> refillTree |> ignore))
                subscriptions.Add(padTreeView.Selection.Changed.Subscribe(fun _ -> jumpToDeclaration false))
                subscriptions.Add(padTreeView.RowActivated.Subscribe(fun _ -> jumpToDeclaration true))

                let sw = new CompactScrolledWindow()
                sw.Add padTreeView
                sw.ShowAll()
                sw :> Widget

        member x.GetToolbarWidgets() = [] :> _

        member x.ReleaseOutlineWidget() =
            treeView |> Option.iter(fun tv ->
                Option.tryCast<ScrolledWindow>(tv.Parent) 
                |> Option.iter (fun sw -> sw.Destroy())

                match tv.Model with
                :? TreeStore as ts -> ts.Dispose()
                | _ -> ())

            treeView <- None
