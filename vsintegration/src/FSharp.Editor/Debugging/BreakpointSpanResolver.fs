//
// BreakpointSpanResolver.fs
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

open System
open System.Threading.Tasks

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.VisualStudio.Text

open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Range

open MonoDevelop.Debugger
open MonoDevelop.Ide.Gui.Documents

// The breakpoint span resolver is using Mono.Addins rather than MEF
[<ExportDocumentControllerExtension(MimeType = "text/x-fsharp")>]
type internal BreakpointSpanResolver() =
    inherit DocumentControllerExtension()

    static let userOpName = "BreakpointResolution"
   
    let getCheckerService(document: Document) =
        document.Project.Solution.Workspace.Services.GetService<FSharpCheckerWorkspaceService>()

    let fsharpRangeToSpan(sourceText: SourceText, range: range) =
        let startPosition = sourceText.Lines.[max 0 (range.StartLine - 1)].Start + range.StartColumn
        let endPosition = sourceText.Lines.[min (range.EndLine - 1) (sourceText.Lines.Count - 1)].Start + range.EndColumn
        Span(startPosition, endPosition - startPosition)

    member x.SupportsController(controller: DocumentController) =
        Task.FromResult(controller.GetContent<ITextBuffer>() <> null)

    static member GetBreakpointLocation(checker: FSharpChecker, sourceText: SourceText, fileName: string, position: int, parsingOptions: FSharpParsingOptions) = 
        async {
            let textLinePos = sourceText.Lines.GetLinePosition(position)
            let textInLine = sourceText.GetSubText(sourceText.Lines.[textLinePos.Line].Span).ToString()

            if String.IsNullOrWhiteSpace textInLine then
                return None
            else
                let textLineColumn = textLinePos.Character
                let fcsTextLineNumber = Line.fromZ textLinePos.Line // Roslyn line numbers are zero-based, FSharp.Compiler.Service line numbers are 1-based
                let! parseResults = checker.ParseFile(fileName, sourceText.ToFSharpSourceText(), parsingOptions, userOpName = userOpName)
                return parseResults.ValidateBreakpointLocation(mkPos fcsTextLineNumber textLineColumn)
        }

    interface IBreakpointSpanResolver with
        member x.GetBreakpointSpanAsync(buffer, position, cancellationToken) =
            let getLineSpan() =
                buffer.CurrentSnapshot.GetLineFromPosition(max 0 (min position (buffer.CurrentSnapshot.Length - 1))).Extent.Span

            asyncMaybe {
                let! document = buffer.CurrentSnapshot.GetRelatedDocumentsWithChanges() |> Seq.tryHead
                let checkerService = getCheckerService document
                let projectInfoManager = checkerService.FSharpProjectOptionsManager
                let! parsingOptions, _options = projectInfoManager.TryGetOptionsForEditingDocumentOrProject(document, cancellationToken)
                let sourceText = buffer.AsTextContainer().CurrentText
                let! range = BreakpointSpanResolver.GetBreakpointLocation(checkerService.Checker, sourceText, document.Name, position, parsingOptions)
                return fsharpRangeToSpan(sourceText, range)
            }
            |> Async.map (Option.defaultWith getLineSpan)
            |> RoslynHelpers.StartAsyncAsTask cancellationToken
