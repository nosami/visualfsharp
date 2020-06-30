// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Collections.Immutable

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Completion
open Microsoft.CodeAnalysis.ExternalAccess.FSharp
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Completion
open System.ComponentModel.Composition
open Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
open Microsoft.VisualStudio.Text.Editor
open System.Threading.Tasks

open System.Collections.Generic;
open Microsoft.CodeAnalysis.Classification
open Microsoft.CodeAnalysis.Text;
open Microsoft.VisualStudio.Text;
open Microsoft.VisualStudio.Text.Adornments;

open FSharp.Compiler.SourceCodeServices

type internal FSharpCompletionService
    (
        workspace: Workspace,
        checkerProvider: FSharpCheckerProvider,
        projectInfoManager: FSharpProjectOptionsManager,
        assemblyContentProvider: AssemblyContentProvider,
        settings: EditorOptions
    ) =
    inherit CompletionServiceWithProviders(workspace)

    let builtInProviders = 
        ImmutableArray.Create<CompletionProvider>(
            FSharpCompletionProvider(workspace, checkerProvider, projectInfoManager, assemblyContentProvider),
            FSharpCommonCompletionProvider.Create(
                HashDirectiveCompletionProvider(workspace, projectInfoManager,
                    [ Completion.Create("""\s*#load\s+(@?"*(?<literal>"[^"]*"?))""", [".fs"; ".fsx"], useIncludeDirectives = true)
                      Completion.Create("""\s*#r\s+(@?"*(?<literal>"[^"]*"?))""", [".dll"; ".exe"], useIncludeDirectives = true)
                      Completion.Create("""\s*#I\s+(@?"*(?<literal>"[^"]*"?))""", ["\x00"], useIncludeDirectives = false) ])))

    override this.Language = FSharpConstants.FSharpLanguageName
    override this.GetBuiltInProviders() = builtInProviders
    override this.GetRules() =
        let enterKeyRule =
            match settings.IntelliSense.EnterKeySetting with
            | NeverNewline -> EnterKeyRule.Never
            | NewlineOnCompleteWord -> EnterKeyRule.AfterFullyTypedWord
            | AlwaysNewline -> EnterKeyRule.Always

        CompletionRules.Default
            .WithDismissIfEmpty(true)
            .WithDismissIfLastCharacterDeleted(true)
            .WithDefaultEnterKeyRule(enterKeyRule)

type internal FSharpCompletionSource
    (textView: ITextView, checkerProvider: FSharpCheckerProvider, projectInfoManager: FSharpProjectOptionsManager, assemblyContentProvider: AssemblyContentProvider) =


    let createParagraphFromLines(lines: List<ClassifiedTextElement>) =
        if lines.Count = 1 then
            // The paragraph contains only one line, so it doesn't need to be added to a container. Avoiding the
            // wrapping container here also avoids a wrapping element in the Cocoa elements used for rendering,
            // improving efficiency.
            lines.[0] :> obj
        else
            // The lines of a multi-line paragraph are stacked to produce the full paragraph.
            ContainerElement(ContainerElementStyle.Stacked, lines |> Seq.map box) :> obj

    let toClassificationTypeName = function
        | TextTags.Keyword ->
            ClassificationTypeNames.Keyword

        | TextTags.Class ->
            ClassificationTypeNames.ClassName

        | TextTags.Delegate ->
            ClassificationTypeNames.DelegateName

        | TextTags.Enum ->
            ClassificationTypeNames.EnumName

        | TextTags.Interface ->
            ClassificationTypeNames.InterfaceName

        | TextTags.Module ->
            ClassificationTypeNames.ModuleName

        | TextTags.Struct ->
            ClassificationTypeNames.StructName

        | TextTags.TypeParameter ->
            ClassificationTypeNames.TypeParameterName

        | TextTags.Field ->
            ClassificationTypeNames.FieldName

        | TextTags.Event ->
            ClassificationTypeNames.EventName

        | TextTags.Label ->
            ClassificationTypeNames.LabelName

        | TextTags.Local ->
            ClassificationTypeNames.LocalName

        | TextTags.Method ->
            ClassificationTypeNames.MethodName

        | TextTags.Namespace ->
            ClassificationTypeNames.NamespaceName

        | TextTags.Parameter ->
            ClassificationTypeNames.ParameterName

        | TextTags.Property ->
            ClassificationTypeNames.PropertyName

        | TextTags.ExtensionMethod ->
            ClassificationTypeNames.ExtensionMethodName

        | TextTags.EnumMember ->
            ClassificationTypeNames.EnumMemberName

        | TextTags.Constant ->
            ClassificationTypeNames.ConstantName

        | TextTags.Alias
        | TextTags.Assembly
        | TextTags.ErrorType
        | TextTags.RangeVariable ->
            ClassificationTypeNames.Identifier

        | TextTags.NumericLiteral ->
            ClassificationTypeNames.NumericLiteral

        | TextTags.StringLiteral ->
            ClassificationTypeNames.StringLiteral

        | TextTags.Space
        | TextTags.LineBreak ->
            ClassificationTypeNames.WhiteSpace

        | TextTags.Operator ->
            ClassificationTypeNames.Operator

        | TextTags.Punctuation ->
            ClassificationTypeNames.Punctuation

        | TextTags.AnonymousTypeIndicator
        | TextTags.Text
        | _ ->
            ClassificationTypeNames.Text


    let buildClassifiedTextElements (taggedTexts:ImmutableArray<TaggedText>) =
        // This method produces a sequence of zero or more paragraphs
        let paragraphs = new List<obj>()

        // Each paragraph is constructed from one or more lines
        let currentParagraph = new List<ClassifiedTextElement>()

        // Each line is constructed from one or more inline elements
        let currentRuns = new List<ClassifiedTextRun>()

        for part in taggedTexts do
            if part.Tag = TextTags.LineBreak then
                if currentRuns.Count > 0 then
                    // This line break means the end of a line within a paragraph.
                    currentParagraph.Add(new ClassifiedTextElement(currentRuns));
                    currentRuns.Clear();
                else
                    // This line break means the end of a paragraph. Empty paragraphs are ignored, but could appear
                    // in the input to this method:
                    //
                    // * Empty <para> elements
                    // * Explicit line breaks at the start of a comment
                    // * Multiple line breaks between paragraphs
                    if currentParagraph.Count > 0 then
                        // The current paragraph is not empty, so add it to the result collection
                        paragraphs.Add(createParagraphFromLines(currentParagraph))
                        currentParagraph.Clear();

            else
                // This is tagged text getting added to the current line we are building.
                currentRuns.Add(new ClassifiedTextRun(part.Tag |> toClassificationTypeName, part.Text))

        if currentRuns.Count > 0 then
            // Add the final line to the final paragraph.
            currentParagraph.Add(new ClassifiedTextElement(currentRuns))

        if currentParagraph.Count > 0 then
            // Add the final paragraph to the result.
            paragraphs.Add(createParagraphFromLines(currentParagraph))

        paragraphs
    /// <summary>
    /// Called when user interacts with expander buttons,
    /// requesting the completion source to provide additional completion items pertinent to the expander button.
    /// For best performance, do not provide <see cref="CompletionContext.Filters"/> unless expansion should add new filters.
    /// Called on a background thread.
    /// </summary>
    /// <param name="session">Reference to the active <see cref="IAsyncCompletionSession"/></param>
    /// <param name="expander">Expander which caused this call</param>
    /// <param name="initialTrigger">What initially caused the completion</param>
    /// <param name="applicableToSpan">Location where completion will take place, on the view's data buffer: <see cref="ITextView.TextBuffer"/></param>
    /// <param name="token">Cancellation token that may interrupt this operation</param>
    /// <returns>A struct that holds completion items and applicable span</returns>
    let commitChars = [|' '; '='; ','; '.'; '<'; '>'; '('; ')'; '!'; ':'; '['; ']'; '|'|].ToImmutableArray()
    interface IAsyncExpandingCompletionSource with
        member __.GetExpandedCompletionContextAsync(session, expander, initialTrigger, applicableToSpan, token) =
            let ctx = Data.CompletionContext.Empty
            Task.FromResult ctx


    interface IAsyncCompletionSource with
        member this.GetCompletionContextAsync(session, trigger, triggerLocation, applicableToSpan, token) =
            async {
                System.Diagnostics.Trace.WriteLine("GetCompletionContextAsync")
                let document = session.TextView.TextSnapshot.GetOpenDocumentInCurrentContextWithChanges()

                let sourceText = session.TextView.TextSnapshot.AsText()
                let! options = projectInfoManager.TryGetOptionsForEditingDocumentOrProject(document, token)
                match options with
                | Some (_parsingOptions, projectOptions) ->
                    let! textVersion = document.GetTextVersionAsync(token) |> liftTaskAsync
                    let getAllSymbols(fileCheckResults: FSharpCheckFileResults) =
                        []
                        //if settings.IntelliSense.IncludeSymbolsFromUnopenedNamespacesOrModules
                        //then assemblyContentProvider.GetAllEntitiesInProjectAndReferencedAssemblies(fileCheckResults)
                        //else []


                    session.TextView.Properties.["PotentialCommitCharacters"] <- commitChars
                    let! completions = FSharpCompletionProvider.ProvideCompletionsAsyncAux(this, checkerProvider.Checker, sourceText, triggerLocation.Position, projectOptions, document.FilePath, textVersion.GetHashCode(), getAllSymbols, (*settings.LanguageServicePerformance*) LanguageServicePerformanceOptions.Default, (*settings.IntelliSense*) IntelliSenseOptions.Default)
                    match completions with
                    | Some completions' ->
                        return new Data.CompletionContext(completions'.ToImmutableArray())
                    | None ->
                        return Data.CompletionContext.Empty
                | _ ->
                    return Data.CompletionContext.Empty
            } |> RoslynHelpers.StartAsyncAsTask token

    /// <summary>
    /// Returns tooltip associated with provided <see cref="CompletionItem"/>.
    /// The returned object will be rendered by <see cref="IViewElementFactoryService"/>. See its documentation for default supported types.
    /// You may export a <see cref="IViewElementFactory"/> to provide a renderer for a custom type.
    /// Since this method is called on a background thread and on multiple platforms, an instance of UIElement may not be returned.
    /// </summary>
    /// <param name="session">Reference to the active <see cref="IAsyncCompletionSession"/></param>
    /// <param name="item"><see cref="CompletionItem"/> which is a subject of the tooltip</param>
    /// <param name="token">Cancellation token that may interrupt this operation</param>
    /// <returns>An object that will be passed to <see cref="IViewElementFactoryService"/>. See its documentation for supported types.</returns>
        member __.GetDescriptionAsync(session, item, token) =
            async {
                let document = session.TextView.TextSnapshot.GetOpenDocumentInCurrentContextWithChanges()
                //let! sourceText = document.GetTextAsync() |> Async.AwaitTask
                let provider = FSharpCompletionProvider(document.Project.Solution.Workspace, checkerProvider, projectInfoManager, assemblyContentProvider)
                let! description = provider.GetDescriptionAsync2(session.TextView, item, token) |> Async.AwaitTask
                let elements = description.TaggedParts |>  buildClassifiedTextElements
                return ContainerElement(ContainerElementStyle.Stacked ||| ContainerElementStyle.VerticalPadding, elements |> Seq.map box) :> obj
                //return elements :> obj
            } |> RoslynHelpers.StartAsyncAsTask token

    /// <summary>
    /// Provides the span applicable to the prospective session.
    /// Called on UI thread and expected to return very quickly, based on syntactic clues.
    /// This method is called as a result of user action, after the Editor makes necessary changes in direct response to user's action.
    /// The state of the Editor prior to making the text edit is captured in <see cref="CompletionTrigger.ViewSnapshotBeforeTrigger"/> of <paramref name="trigger"/>.
    /// This method is called sequentially on available <see cref="IAsyncCompletionSource"/>s until one of them returns
    /// <see cref="CompletionStartData"/> with appropriate level of <see cref="CompletionStartData.Participation"/>
    /// and one returns <see cref="CompletionStartData"/> with <see cref="CompletionStartData.ApplicableToSpan"/>
    /// If neither of the above conditions are met, no completion session will start.
    /// </summary>
    /// <remarks>
    /// If a language service does not wish to participate in completion, it should try to provide a valid <see cref="CompletionStartData.ApplicableToSpan"/>
    /// and set <see cref="CompletionStartData.Participation"/> to <c>false</c>.
    /// This will enable other extensions to provide completion in syntactically appropriate location.
    /// </remarks>
    /// <param name="trigger">What causes the completion, including the character typed and reference to <see cref="ITextView.TextSnapshot"/> prior to triggering the completion</param>
    /// <param name="triggerLocation">Location on the subject buffer that matches this <see cref="IAsyncCompletionSource"/>'s content type</param>
    /// <param name="token">Cancellation token that may interrupt this operation</param>
    /// <returns>Whether this <see cref="IAsyncCompletionSource"/> wishes to participate in completion.</returns>
        member __.InitializeCompletion(trigger, triggerLocation, token) =
            System.Diagnostics.Trace.WriteLine("initialize")
            use _logBlock = Logger.LogBlock LogEditorFunctionId.Completion_ShouldTrigger

            let document = triggerLocation.Snapshot.GetOpenDocumentInCurrentContextWithChanges()

            let getInfo() =
                let defines = projectInfoManager.GetCompilationDefinesForEditingDocument(document)
                (document.Id, document.FilePath, defines)
            

            let sourceText = triggerLocation.Snapshot.AsText()
            let shouldTrigger =
                FSharpCompletionProvider.ShouldTriggerCompletionAux(sourceText, triggerLocation.Position, trigger, getInfo, (*settings.IntelliSense*) IntelliSenseOptions.Default)

            match shouldTrigger with
            | false ->
                Data.CompletionStartData.DoesNotParticipateInCompletion
            | true ->
                Data.CompletionStartData(
                    participation = Data.CompletionParticipation.ProvidesItems,
                    applicableToSpan = new SnapshotSpan(
                        triggerLocation.Snapshot,
                        CompletionUtils.getCompletionItemSpan sourceText triggerLocation.Position))

[<Export(typeof<IAsyncCompletionSourceProvider>)>]
[<Export(typeof<IAsyncCompletionCommitManagerProvider>)>]
[<Microsoft.VisualStudio.Utilities.Name("FSharp Completion Source Provider")>]
[<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpContentType)>]
type internal CompletionSourceProvider
    [<ImportingConstructor>] 
    (
        checkerProvider: FSharpCheckerProvider,
        projectInfoManager: FSharpProjectOptionsManager,
        assemblyContentProvider: AssemblyContentProvider
    ) =

    interface IAsyncCompletionSourceProvider with
        member __.GetOrCreate(textView) =
            System.Diagnostics.Trace.WriteLine("Completion .ctor")
            new FSharpCompletionSource(textView, checkerProvider, projectInfoManager, assemblyContentProvider) :> _

    interface IAsyncCompletionCommitManagerProvider with
        member __.GetOrCreate(_textView) =
            System.Diagnostics.Trace.WriteLine("GetOrCreate FSharpAsyncCompletionCommitManager")
            FSharpAsyncCompletionCommitManager() :> _
