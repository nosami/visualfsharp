//
// InteractivePadKeyHandlers.fs
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
// THE SOFTWARE.
namespace Microsoft.VisualStudio.FSharp.Editor

open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor
open System.ComponentModel.Composition
open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Text.Editor.Commanding.Commands
open Microsoft.VisualStudio.Commanding
open Microsoft.VisualStudio.Text

[<Microsoft.VisualStudio.Utilities.Name("InteractivePadCompletionReturn")>]
[<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpInteractiveContentType)>]
[<Export(typeof<ICommandHandler>)>]
type InteractivePadCompletionReturnHandler
    [<ImportingConstructor>]
    ( completionBroker:ICompletionBroker,
      signatureHelpBroker:ISignatureHelpBroker ) =
    interface ICommandHandler<ReturnKeyCommandArgs> with
        member x.DisplayName = "InteractivePadKeyReturnHandler"
        member x.GetCommandState _args = CommandState.Available

        member x.ExecuteCommand(args, context) =
            let textView = args.TextView
            signatureHelpBroker.DismissAllSessions(textView)
            if completionBroker.IsCompletionActive(textView) then
                false
            else
            let (controller: InteractivePadController) = downcast textView.Properties.[typeof<InteractivePadController>]

            let textBuffer = textView.TextBuffer
            let snapshot = textBuffer.CurrentSnapshot
            let position = textView.Caret.Position.BufferPosition.Position
            let line = snapshot.GetLineFromPosition(position)

            if line.Length > 0 then
                let start = line.Start.Position
                let finish = line.End.Position
                let start = min start finish
                let span = Span(start, finish - start)
                let text = snapshot.GetText(span)
                controller.FsiOutput "\n"
                controller.FsiInput text
            true

[<Microsoft.VisualStudio.Utilities.Name("InteractivePadTypeChar")>]
[<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpInteractiveContentType)>]
[<Export(typeof<ICommandHandler>)>]
type InteractivePadCompletionTypeCharHandler
    [<ImportingConstructor>]
    ( completionBroker:ICompletionBroker,
      signatureHelpBroker:ISignatureHelpBroker ) =
    interface ICommandHandler<TypeCharCommandArgs> with
        member x.DisplayName = "InteractivePadTypeCharHandler"
        member x.GetCommandState _args = CommandState.Available

        member x.ExecuteCommand(args, _context) =
            if args.TypedChar <> '(' && args.TypedChar <> ',' && args.TypedChar <> ' ' then
                signatureHelpBroker.DismissAllSessions(args.TextView)
            let textView = args.TextView
            let (controller: InteractivePadController) = downcast textView.Properties.[typeof<InteractivePadController>]
            controller.EnsureLastLine()
            false

[<Microsoft.VisualStudio.Utilities.Name("InteractivePadCompletionBackspace")>]
[<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpInteractiveContentType)>]
[<Export(typeof<ICommandHandler>)>]
type InteractivePadCompletionBackspaceHandler
    [<ImportingConstructor>]
    ( completionBroker:ICompletionBroker ) =

    interface ICommandHandler<BackspaceKeyCommandArgs> with
        member x.DisplayName = "InteractivePadKeyBackspaceHandler"
        member x.GetCommandState _args = CommandState.Available

        member x.ExecuteCommand(args, _context) =
            let textView = args.TextView
            let snapshot = textView.TextBuffer.CurrentSnapshot
            let lineCount = snapshot.LineCount

            if lineCount > 0 then
                let line = snapshot.GetLineFromLineNumber(lineCount - 1)
                if textView.Caret.Position.BufferPosition.Position > line.Start.Position then
                    false
                else
                    true
            else
                true

[<Microsoft.VisualStudio.Utilities.Name("InteractivePadCompletionUp")>]
[<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpInteractiveContentType)>]
[<Export(typeof<ICommandHandler>)>]
type InteractivePadCompletionUpHandler
    [<ImportingConstructor>]
    ( completionBroker:ICompletionBroker,
      signatureHelpBroker:ISignatureHelpBroker ) =
    interface ICommandHandler<UpKeyCommandArgs> with
        member x.DisplayName = "InteractivePadKeyUpHandler"
        member x.GetCommandState _args = CommandState.Available

        member x.ExecuteCommand(args, context) =
            if signatureHelpBroker.IsSignatureHelpActive(args.TextView) then
                false
            else if completionBroker.IsCompletionActive(args.TextView) then
                false
            else
            let textView = args.TextView
            let (controller: InteractivePadController) = downcast textView.Properties.[typeof<InteractivePadController>]
            controller.HistoryUp()
            true

[<Microsoft.VisualStudio.Utilities.Name("InteractivePadCompletionDown")>]
[<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpInteractiveContentType)>]
[<Export(typeof<ICommandHandler>)>]
type InteractivePadCompletionDownHandler
    [<ImportingConstructor>]
    ( completionBroker:ICompletionBroker,
      signatureHelpBroker:ISignatureHelpBroker ) =
    interface ICommandHandler<DownKeyCommandArgs> with
        member x.DisplayName = "InteractivePadKeyDownHandler"
        member x.GetCommandState _args = CommandState.Available

        member x.ExecuteCommand(args, context) =
            if signatureHelpBroker.IsSignatureHelpActive(args.TextView) then
                false
            else if completionBroker.IsCompletionActive(args.TextView) then
                false
            else
            let textView = args.TextView
            let (controller: InteractivePadController) = downcast textView.Properties.[typeof<InteractivePadController>]
            controller.HistoryDown()
            true
