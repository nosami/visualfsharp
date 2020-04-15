﻿//
// InteractiveWorkspace.fs
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
namespace FSharp.Editor

open Microsoft.CodeAnalysis
open MonoDevelop.Ide.Composition
open System

open System
open System.Collections.Concurrent
open System.Collections.Immutable
open System.ComponentModel.Composition
open System.IO
open System.Linq
open System.Threading
open System.Threading.Tasks
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open Microsoft.CodeAnalysis.Host
open Microsoft.CodeAnalysis.Scripting
open Microsoft.CodeAnalysis.Text
open Microsoft.VisualStudio.Composition
open Microsoft.VisualStudio.Text
open Mono.Addins
open MonoDevelop.Core
open MonoDevelop.Ide.Composition
type InteractiveWorkspace() =
    inherit Workspace(CompositionManager.Instance.HostServices, WorkspaceKind.MiscellaneousFiles) with
        member x.CreateDocument(buffer: ITextBuffer) =
            let projectId = ProjectId.CreateNewId()
            let name = "interactive.fsx"
            let documentId = DocumentId.CreateNewId(projectId, name)
            let container = buffer.AsTextContainer()

            let projectInfo = 
                ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    name = name,
                    assemblyName = "interactive.dll",
                    language = "F# Interactive")

            base.OnProjectAdded(projectInfo)
            let documentInfo =
                DocumentInfo.Create(
                    documentId,
                    name,
                    Array.empty<string>,
                    sourceCodeKind = SourceCodeKind.Script,
                    filePath = name,
                    loader = TextLoader.From(buffer.AsTextContainer(), VersionStamp.Create()))

            base.OnDocumentAdded(documentInfo)
            base.OnDocumentOpened(documentId, container)
