// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Collections.Generic
open System.Diagnostics
open System.Threading

open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Classification
open Microsoft.CodeAnalysis.Editor
open Microsoft.CodeAnalysis.Host.Mef
open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Classification
open Microsoft.VisualStudio.Text.Classification
open System.Windows.Media
open MonoDevelop.Core
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor
// IEditorClassificationService is marked as Obsolete, but is still supported. The replacement (IClassificationService)
// is internal to Microsoft.CodeAnalysis.Workspaces which we don't have internals visible to. Rather than add yet another
// IVT, we'll maintain the status quo.
#nowarn "44"

open FSharp.Compiler.SourceCodeServices

[<RequireQualifiedAccess>]
module internal FSharpClassificationTypes =
    let [<Literal>] Function = ClassificationTypeNames.MethodName
    let [<Literal>] MutableVar = "mutable name"
    let [<Literal>] Printf = ClassificationTypeNames.MethodName
    let [<Literal>] ReferenceType = ClassificationTypeNames.ClassName
    let [<Literal>] Module = ClassificationTypeNames.ClassName
    let [<Literal>] ValueType = ClassificationTypeNames.StructName
    let [<Literal>] Keyword = ClassificationTypeNames.Keyword
    let [<Literal>] Enum = ClassificationTypeNames.EnumName
    let [<Literal>] Property = ClassificationTypeNames.PropertyName
    let [<Literal>] Interface = ClassificationTypeNames.InterfaceName
    let [<Literal>] TypeArgument = ClassificationTypeNames.TypeParameterName
    let [<Literal>] Operator = ClassificationTypeNames.Operator
    let [<Literal>] Disposable = ClassificationTypeNames.ClassName

    let getClassificationTypeName = function
        | SemanticClassificationType.ReferenceType -> ReferenceType
        | SemanticClassificationType.Module -> Module
        | SemanticClassificationType.ValueType -> ValueType
        | SemanticClassificationType.Function -> Function
        | SemanticClassificationType.MutableVar ->
            if PropertyService.Get("FSharpBinding.HighlightMutables", false) then
                MutableVar
            else
                ClassificationTypeNames.LocalName
        | SemanticClassificationType.Printf -> Printf
        | SemanticClassificationType.ComputationExpression
        | SemanticClassificationType.IntrinsicFunction -> Keyword
        | SemanticClassificationType.UnionCase
        | SemanticClassificationType.Enumeration -> Enum
        | SemanticClassificationType.Property -> Property
        | SemanticClassificationType.Interface -> Interface
        | SemanticClassificationType.TypeArgument -> TypeArgument
        | SemanticClassificationType.Operator -> Operator 
        | SemanticClassificationType.Disposable -> Disposable

[<Export(typeof<IFSharpClassificationService>)>]
type internal FSharpClassificationService
    [<ImportingConstructor>]
    (
        checkerProvider: FSharpCheckerProvider,
        projectInfoManager: FSharpProjectOptionsManager
    ) =
    static let userOpName = "SemanticColorization"

    interface IFSharpClassificationService with
       
        member __.AddLexicalClassifications(sourceText: SourceText, textSpan: TextSpan, result: List<ClassifiedSpan>, cancellationToken: CancellationToken) =
            result.AddRange(Tokenizer.getClassifiedSpans(DocumentId.CreateNewId(ProjectId.CreateNewId()), sourceText, textSpan, Some("fake.fs"), [], cancellationToken))
        
        member __.AddSyntacticClassificationsAsync(document: Document, textSpan: TextSpan, result: List<ClassifiedSpan>, cancellationToken: CancellationToken) =
            async {
                use _logBlock = Logger.LogBlock(LogEditorFunctionId.Classification_Syntactic)

                let defines = projectInfoManager.GetCompilationDefinesForEditingDocument(document)  
                let! sourceText = document.GetTextAsync(cancellationToken)  |> Async.AwaitTask
                result.AddRange(Tokenizer.getClassifiedSpans(document.Id, sourceText, textSpan, Some(document.FilePath), defines, cancellationToken))
            } |> RoslynHelpers.StartAsyncUnitAsTask cancellationToken

        member __.AddSemanticClassificationsAsync(document: Document, textSpan: TextSpan, result: List<ClassifiedSpan>, cancellationToken: CancellationToken) =
            asyncMaybe {
                use _logBlock = Logger.LogBlock(LogEditorFunctionId.Classification_Semantic)

                let! _, _, projectOptions = projectInfoManager.TryGetOptionsForDocumentOrProject(document, cancellationToken)
                let! sourceText = document.GetTextAsync(cancellationToken)
                let! _, _, checkResults = checkerProvider.Checker.ParseAndCheckDocument(document, projectOptions, sourceText = sourceText, allowStaleResults = false, userOpName=userOpName) 
                // it's crucial to not return duplicated or overlapping `ClassifiedSpan`s because Find Usages service crashes.
                let targetRange = RoslynHelpers.TextSpanToFSharpRange(document.FilePath, textSpan, sourceText)
                let classificationData = checkResults.GetSemanticClassification (Some targetRange)
                
                for struct (range, classificationType) in classificationData do
                    match RoslynHelpers.TryFSharpRangeToTextSpan(sourceText, range) with
                    | None -> ()
                    | Some span -> 
                        let span = 
                            match classificationType with
                            | SemanticClassificationType.Printf -> span
                            | _ -> Tokenizer.fixupSpan(sourceText, span)
                        result.Add(ClassifiedSpan(span, FSharpClassificationTypes.getClassificationTypeName(classificationType)))
            } 
            |> Async.Ignore |> RoslynHelpers.StartAsyncUnitAsTask cancellationToken

        // Do not perform classification if we don't have project options (#defines matter)
        member __.AdjustStaleClassification(_: SourceText, classifiedSpan: ClassifiedSpan) : ClassifiedSpan = classifiedSpan
