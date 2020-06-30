// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Threading

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor

open System.ComponentModel.Composition;

[<Export(typeof<IFSharpGoToDefinitionService>)>]
[<Export(typeof<FSharpGoToDefinitionService>)>]
type internal FSharpGoToDefinitionService 
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider,
        projectInfoManager: FSharpProjectOptionsManager
    ) =

    let gtd = GoToDefinition(checkerProvider.Checker, projectInfoManager)
    let statusBar = StatusBar()  
   
    interface IFSharpGoToDefinitionService with
        /// Invoked with Peek Definition.
        member __.FindDefinitionsAsync (document: Document, position: int, cancellationToken: CancellationToken) =
            gtd.FindDefinitionsForPeekTask(document, position, cancellationToken)

        /// Invoked with Go to Definition.
        /// Try to navigate to the definiton of the symbol at the symbolRange in the originDocument
        member __.TryGoToDefinition(document: Document, position: int, cancellationToken: CancellationToken) =
            let computation =
                async {
                
                    statusBar.Message(SR.LocatingSymbol())
                    use __ = statusBar.Animate()

                    let! position  = gtd.FindDefinitionAtPosition(document, position)

                    match position with
                    | Some (item, _) ->
                        gtd.NavigateToItem(item, statusBar)
                    | None ->
                        statusBar.TempMessage (SR.CannotDetermineSymbol())
                }
            Async.StartImmediate(computation, cancellationToken)
            true