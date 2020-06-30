
//
// InteractivePad.fs
//
// Author:
//       jasonimison <jaimison@microsoft.com>
//
// Copyright (c) 2020 Microsoft
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Collections.Generic
open System.ComponentModel.Composition
open System.IO
open FSharp.Editor

open Gtk

open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Tagging
open MonoDevelop.Components
open MonoDevelop.Components.Commands
open MonoDevelop.Components.Docking
open MonoDevelop.Core
open MonoDevelop.Core.Execution
open MonoDevelop.FSharp
open MonoDevelop.Ide
open MonoDevelop.Ide.Composition

type FSharpCommands =
    | ShowFSharpInteractive = 0
    | SendSelection = 1
    | SendLine = 2
    | SendFile = 3

type KillIntent =
    | Restart
    | Kill
    | NoIntent // Unexpected kill, or from #q/#quit, so we prompt

type ShellHistory() =
    let history = ResizeArray<string>()
    let mutable nextUp = 0
    let mutable nextDown = 0

    member x.Push command =
        history.Add command
        nextUp <- history.Count - 1
        nextDown <- history.Count - 1

    member x.Up() =
        match nextUp with
        | -1 -> None
        | index when index >= history.Count -> None
        | index ->
            nextDown <- nextUp
            nextUp <- nextUp - 1
            Some history.[index]

    member x.Down() =
        if nextDown = history.Count then
            None
        else
            nextUp <- nextDown
            nextDown <- nextDown + 1
            if nextDown = history.Count then
                None
            else
                Some history.[nextDown]


[<Microsoft.VisualStudio.Utilities.BaseDefinition("text")>]
[<Microsoft.VisualStudio.Utilities.Name(FSharpContentTypeNames.FSharpInteractiveContentType)>]
[<System.ComponentModel.Composition.Export>]
type InteractivePadController(session: InteractiveSession) as this =
    let mutable view = null
    let contentTypeRegistry = CompositionManager.Instance.GetExportedValue<Microsoft.VisualStudio.Utilities.IContentTypeRegistryService>()
    let textBufferFactory = CompositionManager.Instance.GetExportedValue<ITextBufferFactoryService>()
    let factory = CompositionManager.Instance.GetExportedValue<ICocoaTextEditorFactoryService>()
    let contentType = contentTypeRegistry.GetContentType(FSharpContentTypeNames.FSharpInteractiveContentType)

    let roles = factory.CreateTextViewRoleSet(PredefinedTextViewRoles.Editable, PredefinedTextViewRoles.Interactive, PredefinedTextViewRoles.Document)
    let textBuffer = textBufferFactory.CreateTextBuffer("", contentType)
    
    let textView = factory.CreateTextView(textBuffer, roles)

    let workspace = new InteractiveWorkspace()
    let history = ShellHistory()
    do
        //resourceDictionary.[ClassificationFormatDefinition.TypefaceId] <- TextField.Font
        //resourceDictionary.[ClassificationFormatDefinition.FontRenderingSizeId] <- 20
        //resourceDictionary.[ClassificationFormatDefinition.BackgroundBrushId] <- System.Windows.Media.Brushes.Black
        //resourceDictionary.[ClassificationFormatDefinition.ForegroundColorId] <- System.Windows.Media.Brushes.White
        //editorFormat.SetProperties("Plain Text", resourceDictionary)

        textView.Options.SetOptionValue(DefaultTextViewOptions.UseVisibleWhitespaceId, false)
        textView.Options.SetOptionValue(DefaultTextViewHostOptions.ChangeTrackingId, false)
        textView.Options.SetOptionValue(DefaultTextViewHostOptions.LineNumberMarginId, false)
        textView.Options.SetOptionValue(DefaultTextViewHostOptions.OutliningMarginId, false)
        textView.Options.SetOptionValue(DefaultTextViewHostOptions.GlyphMarginId, true)
        textView.VisualElement.TranslatesAutoresizingMaskIntoConstraints <- false
        textView.Properties.[typeof<InteractivePadController>] <- this
        textView.Properties.[typeof<InteractiveSession>] <- session
        let host = factory.CreateTextViewHost(textView, true)
        view <- host.HostControl
        workspace.CreateDocument(textBuffer)

    let getActiveDocumentFileName () =
        if IdeApp.Workbench.ActiveDocument <> null && FileService.isInsideFSharpFile() then
            let docFileName = IdeApp.Workbench.ActiveDocument.FileName.ToString()
            if docFileName <> null then
                let directoryName = Path.GetDirectoryName docFileName
                Some docFileName
            else None
        else None

    let inputLines = HashSet<int>()

    let getLastLine() =
         let snapshot = textBuffer.CurrentSnapshot
         let lineCount = snapshot.LineCount

         if lineCount > 0 then
             Some (snapshot.GetLineFromLineNumber(lineCount - 1))
         else
             None

    let setCaretLine text =
        getLastLine() |> Option.iter(fun line ->
             use edit = textBuffer.CreateEdit()

             if edit.Replace(new Span(line.Start.Position, line.Length), text) then
                edit.Apply() |> ignore)

    let scrollToLastLine() =
        getLastLine() |> Option.iter(fun line ->
             let snapshotSpan = new SnapshotSpan(line.Start, 0)
             textView.ViewScroller.EnsureSpanVisible(snapshotSpan))

    let mutable readOnlyRegion = None

    let updateReadOnlyRegion() =
        getLastLine() |> Option.iter(fun line ->
            use edit = textBuffer.CreateReadOnlyRegionEdit()

            readOnlyRegion |> Option.iter(fun region -> edit.RemoveReadOnlyRegion region)
            readOnlyRegion <- edit.CreateReadOnlyRegion(new Span(0, line.Start.Position - 1)) |> Some

            edit.Apply() |> ignore)

    member this.View = view
    member this.Session = session

    member this.IsInputLine(line:int) =
        let buffer = textView.TextBuffer
        inputLines.Contains line

    member this.FsiInput text =
        let fileName = getActiveDocumentFileName()
        history.Push text
        let buffer = textView.TextBuffer
        session.SendInput (text + "\n") fileName

    member this.FsiOutput text =
        let buffer = textView.TextBuffer
        use edit = buffer.CreateEdit()
        let position = buffer.CurrentSnapshot.Length

        if edit.Insert(position, text) then
            edit.Apply() |> ignore
            scrollToLastLine()
            updateReadOnlyRegion()

    member this.Clear() =
        inputLines.Clear()
        use readOnlyEdit = textBuffer.CreateReadOnlyRegionEdit()
        readOnlyRegion |> Option.iter(fun region -> readOnlyEdit.RemoveReadOnlyRegion region)
        readOnlyRegion <- None
        readOnlyEdit.Apply() |> ignore

        use edit = textView.TextBuffer.CreateEdit()
        edit.Delete(0, textView.TextBuffer.CurrentSnapshot.Length) |> ignore
        edit.Apply() |> ignore

    member this.SetPrompt() =
        this.FsiOutput "\n"
        let buffer = textView.TextBuffer
        let snapshot = buffer.CurrentSnapshot
        let lastLine = snapshot.GetLineFromLineNumber(snapshot.LineCount - 1)
        let glyphTagger = InteractiveGlyphManagerService.interactiveGlyphTagger(textView)
        inputLines.Add(snapshot.LineCount - 1) |> ignore

        glyphTagger.AddPrompt lastLine.Start.Position
        scrollToLastLine()
        updateReadOnlyRegion()

    member this.HistoryUp() =
        history.Up() |> Option.iter setCaretLine

    member this.HistoryDown() =
        history.Down()
        |> function Some c -> setCaretLine c | None -> setCaretLine ""

    member this.EnsureLastLine() =
        getLastLine() |> Option.iter(fun line ->
            if textView.Caret.Position.BufferPosition.Position < line.Start.Position then
                textView.Caret.MoveTo(line.End) |> ignore)

[<Export(typeof<IViewTaggerProvider>)>]
[<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpInteractiveContentType)>]
[<TagType(typeof<InteractivePromptGlyphTag>)>]
type InteractivePromptGlyphTaggerProvider() =
    interface IViewTaggerProvider with
        member x.CreateTagger(textView, buffer) =
            box(InteractivePromptGlyphTagger textView) :?> _

type FSharpInteractivePad() as this =
    inherit MonoDevelop.Ide.Gui.PadContent()

    let mutable killIntent = NoIntent
    let mutable activeDoc : IDisposable option = None
    let mutable lastLineOutput = None

    let promptIcon = ImageService.GetIcon("md-breadcrumb-next")
    let newLineIcon = ImageService.GetIcon("md-template")

    let getActiveDocumentFileName () =
        if IdeApp.Workbench.ActiveDocument <> null && FileService.isInsideFSharpFile() then
            let docFileName = IdeApp.Workbench.ActiveDocument.FileName.ToString()
            if docFileName <> null then
                let directoryName = Path.GetDirectoryName docFileName
                Some docFileName
            else None
        else None

    let input = new ResizeArray<_>()

    let setupSession() =
        let pathToExe =
            Path.Combine(Reflection.Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName, "MonoDevelop.FSharpInteractive.Service.exe")
            |> ProcessArgumentBuilder.Quote
        let ses = InteractiveSession(pathToExe)
        let controller = new InteractivePadController(ses)
        this.Controller <- Some controller
        this.Host <- new GtkNSViewHost(controller.View)
        this.Host.ShowAll()
        input.Clear()
        ses.TextReceived.Add(fun t -> 
            Runtime.RunInMainThread(fun () -> controller.FsiOutput t) |> ignore)
        ses.PromptReady.Add(fun () -> Runtime.RunInMainThread(fun () -> controller.SetPrompt() ) |> ignore)
        ses

    let mutable session = None

    let resetFsi intent =
        input.Clear()
        killIntent <- intent
        this.Controller |> Option.iter (fun controller -> controller.Clear())
        session |> Option.iter (fun (ses:InteractiveSession) -> ses.Restart())

    member x.Session = session

    member x.Shutdown()  =
        do LoggingService.LogDebug ("Interactive: Shutdown()!")
        resetFsi Kill

    member x.SendCommand command =
        let fileName = getActiveDocumentFileName()

        input.Add command
        session
        |> Option.iter(fun ses ->
            ses.SendInput (command + ";;") fileName)

    override x.Dispose() =
        LoggingService.LogDebug ("Interactive: disposing pad...")
        activeDoc |> Option.iter (fun ad -> ad.Dispose())
        x.Shutdown()

    override x.Control = Control.op_Implicit x.Host
 
    static member Pad =
        try let pad = IdeApp.Workbench.GetPad<FSharpInteractivePad>()
            
            if pad <> null then Some(pad)
            else
                //*attempt* to add the pad manually this seems to fail sporadically on updates and reinstalls, returning null
                let pad = IdeApp.Workbench.AddPad(new FSharpInteractivePad(),
                                                  "FSharp.MonoDevelop.FSharpInteractivePad",
                                                  "F# Interactive",
                                                  "Center Bottom",
                                                  IconId("md-fs-project"))
                if pad <> null then Some(pad)
                else None
        with exn -> None

    static member BringToFront(grabfocus) =
        FSharpInteractivePad.Pad |> Option.iter (fun pad -> pad.BringToFront(grabfocus))

    static member Fsi =
        FSharpInteractivePad.Pad |> Option.bind (fun pad -> Some(pad.Content :?> FSharpInteractivePad))

    member x.LastOutputLine
        with get() = lastLineOutput
        and set value = lastLineOutput <- value

    member x.SendSelection() =
        if x.IsSelectionNonEmpty then
            let textView = IdeApp.Workbench.ActiveDocument.GetContent<ITextView>()
            for span in textView.Selection.VirtualSelectedSpans do
                x.SendCommand (span.GetText())
        else
          //if nothing is selected send the whole line
            x.SendLine()

    member x.SendLine() =
        if isNull IdeApp.Workbench.ActiveDocument then ()
        else
            let view = IdeApp.Workbench.ActiveDocument.GetContent<ITextView>();
            let line = view.Caret.Position.BufferPosition.GetContainingLine();
            let text = line.GetText()
            x.SendCommand text

    member x.SendFile() =
        let text = IdeApp.Workbench.ActiveDocument.TextBuffer.CurrentSnapshot.GetText()
        x.SendCommand text

    member x.IsSelectionNonEmpty =
        if isNull IdeApp.Workbench.ActiveDocument ||
            isNull IdeApp.Workbench.ActiveDocument.FileName.FileName then false
        else
            let textView = IdeApp.Workbench.ActiveDocument.GetContent<ITextView>()
            not(textView.Selection.IsEmpty)

    member x.LoadReferences(project:FSharpProject) =
        LoggingService.LogDebug ("FSI:  #LoadReferences")
        async {
            let! orderedReferences = project.GetOrderedReferences (CompilerArguments.getConfig())
            orderedReferences |> List.iter (fun a -> x.SendCommand (sprintf  @"#r ""%s""" a.Path))
        } |> Async.StartImmediate

    member val Host:GtkNSViewHost = null with get, set
    member val Controller:InteractivePadController option = None with get, set

    override x.Initialize(container:MonoDevelop.Ide.Gui.IPadWindow) =
        LoggingService.LogDebug ("InteractivePad: created!")
        let toolbar = container.GetToolbar(DockPositionType.Right)

        let addButton(icon, action, tooltip) =
            let button = new DockToolButton(icon)
            button.Clicked.Add(action)
            button.TooltipText <- tooltip
            toolbar.Add(button)

        addButton ("gtk-save", (fun _ -> x.Save()), GettextCatalog.GetString ("Save as script"))
        addButton ("gtk-open", (fun _ -> x.OpenScript()), GettextCatalog.GetString ("Open"))
        addButton ("gtk-clear", (fun _ -> x.ClearFsi()), GettextCatalog.GetString ("Clear"))
        addButton ("gtk-refresh", (fun _ -> x.RestartFsi()), GettextCatalog.GetString ("Reset"))
        toolbar.ShowAll()
        let ses = setupSession()
        session <- ses |> Some

        container.PadContentShown.Add(fun _args -> if not ses.HasStarted then ses.StartReceiving() |> ignore)

    member x.RestartFsi() = resetFsi Restart

    member x.ClearFsi() = x.Controller |> Option.iter(fun c -> c.Clear())

    member x.Save() =
        let dlg = new MonoDevelop.Ide.Gui.Dialogs.OpenFileDialog(GettextCatalog.GetString ("Save as .fsx"), MonoDevelop.Components.FileChooserAction.Save)

        dlg.DefaultFilter <- dlg.AddFilter (GettextCatalog.GetString ("F# script files"), "*.fsx")
        if dlg.Run () then
            let file = 
                if dlg.SelectedFile.Extension = ".fsx" then
                    dlg.SelectedFile
                else
                    dlg.SelectedFile.ChangeExtension(".fsx")

            let lines = input |> Seq.map (fun line -> line.TrimEnd(';'))
            let fileContent = String.concat "\n" lines
            File.WriteAllText(file.FullPath.ToString(), fileContent)

    member x.OpenScript() =
        let dlg = MonoDevelop.Ide.Gui.Dialogs.OpenFileDialog(GettextCatalog.GetString ("Open script"), MonoDevelop.Components.FileChooserAction.Open)
        dlg.AddFilter (GettextCatalog.GetString ("F# script files"), [|".fs"; "*.fsi"; "*.fsx"; "*.fsscript"; "*.ml"; "*.mli" |]) |> ignore
        if dlg.Run () then
            let file = dlg.SelectedFile
            x.SendCommand ("#load @\"" + file.FullPath.ToString() + "\"")

type InteractiveCommand(command) =
    inherit CommandHandler()

    override x.Run() =
        FSharpInteractivePad.Fsi
        |> Option.iter (fun fsi -> command fsi
                                   FSharpInteractivePad.BringToFront(false))

type FSharpFileInteractiveCommand(command) =
    inherit InteractiveCommand(command)

    override x.Update(info:CommandInfo) =
        info.Enabled <- true
        info.Visible <- FileService.isInsideFSharpFile()

type ShowFSharpInteractive() =
    inherit InteractiveCommand(ignore)
    override x.Update(info:CommandInfo) =
        info.Enabled <- true
        info.Visible <- true

type SendSelection() =
    inherit FSharpFileInteractiveCommand(fun fsi -> fsi.SendSelection())

type SendLine() =
    inherit FSharpFileInteractiveCommand(fun fsi -> fsi.SendLine())

type SendFile() =
    inherit FSharpFileInteractiveCommand(fun fsi -> fsi.SendFile())

type SendReferences() =
    inherit CommandHandler()
    override x.Run() =
        async {
            let project = IdeApp.Workbench.ActiveDocument.Owner :?> FSharpProject
            FSharpInteractivePad.Fsi
            |> Option.iter (fun fsi -> fsi.LoadReferences(project)
                                       FSharpInteractivePad.BringToFront(false))
        } |> Async.StartImmediate

type RestartFsi() =
    inherit InteractiveCommand(fun fsi -> fsi.RestartFsi())

type ClearFsi() =
    inherit InteractiveCommand(fun fsi -> fsi.ClearFsi())
