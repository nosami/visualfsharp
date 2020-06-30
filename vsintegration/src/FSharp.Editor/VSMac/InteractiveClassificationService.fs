//
// InteractiveClassificationService.fs
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

open System.ComponentModel.Composition
open System.Collections.Generic
open System.Threading

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Classification
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Classification

[<Export(typeof<IFSharpInteractiveClassificationService>)>]
type internal FSharpInteractiveClassificationService
    [<ImportingConstructor>]
    (
        service: IFSharpClassificationService
    ) =
    interface IFSharpInteractiveClassificationService with
       
        member __.AddLexicalClassifications(sourceText: SourceText, textSpan: TextSpan, result: List<ClassifiedSpan>, cancellationToken: CancellationToken) =
            ()

        member __.AddSyntacticClassificationsAsync(document: Document, textSpan: TextSpan, result: List<ClassifiedSpan>, cancellationToken: CancellationToken) =
            let classificationTask =
                maybe {
                    match document.TryGetText() with
                    | true, sourceText ->
                        let line = sourceText.Lines.GetLineFromPosition(textSpan.Start).LineNumber
                        let! fsi = FSharpInteractivePad.Fsi
                        let! controller = fsi.Controller
                        if controller.IsInputLine(line) then
                            return! service.AddSyntacticClassificationsAsync(document, textSpan, result, cancellationToken) |> Some
                        else
                            return! None
                    | false, _ -> return! None
                }
            match classificationTask with
            | Some classifications -> classifications
            | None -> Tasks.Task.CompletedTask

        // Do not perform classification if we don't have project options (#defines matter)
        member __.AdjustStaleClassification(_: SourceText, classifiedSpan: ClassifiedSpan) : ClassifiedSpan = classifiedSpan

