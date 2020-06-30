namespace MonoDevelop.FSharp
//
// FSharpPathedDocumentExtensionProvider.fs
//
// Author:
//       jasonimison <jaimison@microsoft.com>
//
// Copyright (c) 2020 Microsoft. All rights reserved.
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


open System.ComponentModel.Composition
open Microsoft.VisualStudio.Text.Editor
open MonoDevelop.TextEditor
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor
open Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.Threading

[<Export(typeof<IEditorContentProvider>)>]
[<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpContentType)>]
[<TextViewRole(PredefinedTextViewRoles.PrimaryDocument)>]
[<Microsoft.VisualStudio.Utilities.Order(Before = "Default")>]
type internal FSharpOutlineDocumentExtensionProvider
    [<ImportingConstructor>]
    (
        fsharpCheckerProvider: FSharpCheckerProvider,
        optionsManager: FSharpProjectOptionsManager,
        joinableTaskContext: JoinableTaskContext
    ) as x =
    inherit EditorContentInstanceProvider<FSharpOutlineDocumentExtension>()

    override x.CreateInstance(view) = new FSharpOutlineDocumentExtension(optionsManager, fsharpCheckerProvider.Checker, view, joinableTaskContext)
