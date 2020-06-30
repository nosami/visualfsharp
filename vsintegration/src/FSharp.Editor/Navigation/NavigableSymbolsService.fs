// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Threading
open System.Threading.Tasks
open System.ComponentModel.Composition

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Navigation

open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor

[<AllowNullLiteral>]
type internal FSharpNavigableSymbol(item: FSharpNavigableItem, span: SnapshotSpan, gtd: GoToDefinition, statusBar: StatusBar) =
    interface INavigableSymbol with
        member __.Navigate(_: INavigableRelationship) =
            gtd.NavigateToItem(item, statusBar)

        member __.Relationships = seq { yield PredefinedNavigableRelationships.Definition }

        member __.SymbolSpan = span

type internal FSharpNavigableSymbolSource(checkerProvider: FSharpCheckerProvider, projectInfoManager: FSharpProjectOptionsManager(*, serviceProvider: IServiceProvider*)) =
    
    let mutable disposed = false
    let gtd = GoToDefinition(checkerProvider.Checker, projectInfoManager)
    let statusBar = StatusBar()

    interface INavigableSymbolSource with
        member __.GetNavigableSymbolAsync(triggerSpan: SnapshotSpan, cancellationToken: CancellationToken) =
            // Yes, this is a code smell. But this is how the editor API accepts what we would treat as None.
            if disposed then null
            else
                asyncMaybe {
                    let snapshot = triggerSpan.Snapshot
                    let position = triggerSpan.Start.Position
                    let document = snapshot.GetOpenDocumentInCurrentContextWithChanges()
                    let! sourceText = document.GetTextAsync() |> liftTaskAsync

                    let gtdTask = gtd.FindDefinitionTask(document, position, cancellationToken)

                    // Wrap this in a try/with as if the user clicks "Cancel" on the thread dialog, we'll be cancelled.
                    // Task.Wait throws an exception if the task is cancelled, so be sure to catch it.
                    try
                        // This call to Wait() is fine because we want to be able to provide the error message in the status bar.
                        gtdTask.Wait()

                        if gtdTask.Status = TaskStatus.RanToCompletion && gtdTask.Result.IsSome then
                            let navigableItem, range = gtdTask.Result.Value

                            let declarationTextSpan = RoslynHelpers.FSharpRangeToTextSpan(sourceText, range)
                            let declarationSpan = Span(declarationTextSpan.Start, declarationTextSpan.Length)
                            let symbolSpan = SnapshotSpan(snapshot, declarationSpan)

                            return FSharpNavigableSymbol(navigableItem, symbolSpan, gtd, statusBar) :> INavigableSymbol
                        else 
                            return null
                    with exc ->
                        return null
                }
                |> Async.map Option.toObj
                |> RoslynHelpers.StartAsyncAsTask cancellationToken
        
        member __.Dispose() =
            disposed <- true

[<Export(typeof<INavigableSymbolSourceProvider>)>]
[<Microsoft.VisualStudio.Utilities.Name("F# Navigable Symbol Service")>]
[<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpContentType)>]
[<Microsoft.VisualStudio.Utilities.Order>]
type internal FSharpNavigableSymbolService
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider,
        projectInfoManager: FSharpProjectOptionsManager
    ) =

    interface INavigableSymbolSourceProvider with
        member __.TryCreateNavigableSymbolSource(_: ITextView, _: ITextBuffer) =
            new FSharpNavigableSymbolSource(checkerProvider, projectInfoManager) :> INavigableSymbolSource