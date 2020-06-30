// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Layout
open FSharp.Compiler.Layout.TaggedTextOps
open System.Collections.Generic
open System.IO
open System.Threading
open Microsoft.CodeAnalysis.Text;
open Microsoft.CodeAnalysis.Text.Shared.Extensions;
open Microsoft.VisualStudio.Core.Imaging;
open Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
open Microsoft.VisualStudio.Text;
open Microsoft.VisualStudio.Text.Adornments;
open Microsoft.VisualStudio.Text.Editor;
open System.Collections.Immutable

type internal ITaggedTextCollector =
    abstract Add: text: TaggedText -> unit
    abstract EndsWithLineBreak: bool
    abstract IsEmpty: bool
    abstract StartXMLDoc: unit -> unit

type internal TextSanitizingCollector(collector, ?lineLimit: int) =
    let mutable isEmpty = true 
    let mutable endsWithLineBreak = false
    let mutable count = 0
    let mutable startXmlDoc = false

    let addTaggedTextEntry (text:TaggedText) =
        match lineLimit with
        | Some lineLimit when lineLimit = count ->
            // add ... when line limit is reached
            collector (tagText "...")
            count <- count + 1
        | _ ->
            isEmpty <- false
            endsWithLineBreak <- text.Tag = LayoutTag.LineBreak
            if endsWithLineBreak then count <- count + 1
            collector text
    
    static let splitTextRegex = Regex(@"\s*\n\s*\n\s*", RegexOptions.Compiled ||| RegexOptions.ExplicitCapture)
    static let normalizeSpacesRegex = Regex(@"\s+", RegexOptions.Compiled ||| RegexOptions.ExplicitCapture)

    let reportTextLines (s: string) =
        // treat _double_ newlines as line breaks and remove all \n after that
        let paragraphs = splitTextRegex.Split(s.Replace("\r", "")) |> Array.filter (not << String.IsNullOrWhiteSpace)
        paragraphs
        |> Array.iteri (fun i paragraph ->
            let paragraph = normalizeSpacesRegex.Replace(paragraph, " ")
            let paragraph = 
                // it's the first line of XML Doc. It often has heading '\n' and spaces, we should remove it.
                // We should not remove them from subsequent lines, because spaces may be proper delimiters 
                // between plane text and formatted code.
                if startXmlDoc then 
                    startXmlDoc <- false
                    paragraph.TrimStart() 
                else paragraph
                
            addTaggedTextEntry (tagText paragraph)
            if i < paragraphs.Length - 1 then
                // insert two line breaks to separate paragraphs
                addTaggedTextEntry Literals.lineBreak
                addTaggedTextEntry Literals.lineBreak)

    interface ITaggedTextCollector with
        member this.Add taggedText = 
            // TODO: bail out early if line limit is already hit
            match taggedText.Tag with
            | LayoutTag.Text -> reportTextLines taggedText.Text
            | _ -> addTaggedTextEntry taggedText

        member this.IsEmpty = isEmpty
        member this.EndsWithLineBreak = isEmpty || endsWithLineBreak
        member this.StartXMLDoc() = startXmlDoc <- true

/// XmlDocumentation builder, using the VS interfaces to build documentation.  An interface is used
/// to allow unit testing to give an alternative implementation which captures the documentation.
type internal IDocumentationBuilder =

    /// Append the given raw XML formatted into the string builder
    abstract AppendDocumentationFromProcessedXML : xmlCollector: ITaggedTextCollector * exnCollector: ITaggedTextCollector * processedXml:string * showExceptions:bool * showParameters:bool * paramName:string option-> unit

    /// Appends text for the given filename and signature into the StringBuilder
    abstract AppendDocumentation : xmlCollector: ITaggedTextCollector * exnCollector: ITaggedTextCollector * filename: string * signature: string * showExceptions: bool * showParameters: bool * paramName: string option-> unit

/// Documentation helpers.
module internal XmlDocumentation =
    open System.Security
    open System.Collections.Generic
    open Internal.Utilities.StructuredFormat
    open Microsoft.CodeAnalysis.Classification
    open FSharp.Compiler
    open Microsoft.VisualStudio.Core.Imaging
    open Microsoft.VisualStudio.Language.StandardClassification
    open Microsoft.VisualStudio.Text.Adornments
    let layoutTagToClassificationTag (layoutTag:LayoutTag) =
        match layoutTag with
        | ActivePatternCase
        | ActivePatternResult
        | UnionCase
        | Enum -> ClassificationTypeNames.EnumName // Roslyn-style classification name
        | Alias
        | Class
        | Module
        | Record
        | Struct
        | TypeParameter
        | Union
        | UnknownType -> PredefinedClassificationTypeNames.Type
        | Interface -> ClassificationTypeNames.InterfaceName // Roslyn-style classification name
        | Keyword -> PredefinedClassificationTypeNames.Keyword
        | Delegate
        | Event
        | Field
        | Local
        | Member
        | Method
        | ModuleBinding
        | Namespace
        | Parameter
        | Property
        | RecordField -> PredefinedClassificationTypeNames.Identifier
        | LineBreak
        | Space -> PredefinedClassificationTypeNames.WhiteSpace
        | NumericLiteral -> PredefinedClassificationTypeNames.Number
        | Operator -> PredefinedClassificationTypeNames.Operator
        | StringLiteral -> PredefinedClassificationTypeNames.String
        | Punctuation
        | Text
        | UnknownEntity -> PredefinedClassificationTypeNames.Other

    let buildContainerElement (itemGroup:ImmutableArray<Layout.TaggedText>) =
        let finalCollection = List<ContainerElement>()
        let currentContainerItems = List<obj>()
        let runsCollection = List<ClassifiedTextRun>()
        let flushRuns() =
            if runsCollection.Count > 0 then
                let element = ClassifiedTextElement(runsCollection)
                currentContainerItems.Add(element :> obj)
                runsCollection.Clear()
        let flushContainer() =
            if currentContainerItems.Count > 0 then
                let element = ContainerElement(ContainerElementStyle.Wrapped, currentContainerItems)
                finalCollection.Add(element)
                currentContainerItems.Clear()
        for item in itemGroup do
            let classificationTag = layoutTagToClassificationTag item.Tag
            match item with
            //| :? NavigableTaggedText as nav when navigation.IsTargetValid nav.Range ->
                //flushRuns()
                //let navigableTextRun = NavigableTextRun(classificationTag, item.Text, fun () -> navigation.NavigateTo nav.Range)
                //currentContainerItems.Add(navigableTextRun :> obj)
            | _ when item.Tag = LineBreak ->
                flushRuns()
                // preserve succesive linebreaks
                if currentContainerItems.Count = 0 then
                    runsCollection.Add(ClassifiedTextRun(PredefinedClassificationTypeNames.Other, System.String.Empty))
                    flushRuns()
                flushContainer()
            | _ -> 
                let newRun = ClassifiedTextRun(classificationTag, item.Text)
                runsCollection.Add(newRun)   
        flushRuns()
        flushContainer()
        ContainerElement(ContainerElementStyle.Stacked, finalCollection |> Seq.map box)

    /// If the XML comment starts with '<' not counting whitespace then treat it as a literal XML comment.
    /// Otherwise, escape it and surround it with <summary></summary>
    let ProcessXml(xml:string) =
        if String.IsNullOrEmpty(xml) then xml
        else
            let trimmedXml = xml.TrimStart([|' ';'\r';'\n'|])
            if trimmedXml.Length > 0 then
                if trimmedXml.[0] <> '<' then 
                    // This code runs for local/within-project xmldoc tooltips, but not for cross-project or .XML - for that see ast.fs in the compiler
                    let escapedXml = SecurityElement.Escape(xml)
                    "<summary>" + escapedXml + "</summary>"
                else 
                    "<root>" + xml + "</root>"
            else xml

    let AppendHardLine(collector: ITaggedTextCollector) =
        collector.Add Literals.lineBreak
       
    let EnsureHardLine(collector: ITaggedTextCollector) =
        if not collector.EndsWithLineBreak then AppendHardLine collector
        
    let AppendOnNewLine (collector: ITaggedTextCollector) (line:string) =
        if line.Length > 0 then 
            EnsureHardLine collector
            collector.Add(TaggedTextOps.tagText line)

    open System.Xml
    open System.Xml.Linq

    let rec private WriteElement (collector: ITaggedTextCollector) (n: XNode) = 
        match n.NodeType with
        | XmlNodeType.Text -> 
            WriteText collector (n :?> XText)
        | XmlNodeType.Element ->
            let el = n :?> XElement
            match el.Name.LocalName with
            | "see" | "seealso" -> 
                for attr in el.Attributes() do
                    WriteAttribute collector attr "cref" (WriteTypeName collector)
            | "paramref" | "typeref" ->
                for attr in el.Attributes() do
                    WriteAttribute collector attr "name" (tagParameter >> collector.Add)
            | _ -> 
                WriteNodes collector (el.Nodes())
        | _ -> ()
                
    and WriteNodes (collector: ITaggedTextCollector) (nodes: seq<XNode>) = 
        for n in nodes do
            WriteElement collector n

    and WriteText (collector: ITaggedTextCollector) (n: XText) = 
        collector.Add(tagText n.Value)

    and WriteAttribute (collector: ITaggedTextCollector) (attr: XAttribute) (taggedName: string) tagger = 
        if attr.Name.LocalName = taggedName then
            tagger attr.Value
        else
            collector.Add(tagText attr.Value)

    and WriteTypeName (collector: ITaggedTextCollector) (typeName: string) =
        let typeName = if typeName.StartsWith("T:") then typeName.Substring(2) else typeName
        let parts = typeName.Split([|'.'|])
        for i = 0 to parts.Length - 2 do
            collector.Add(tagNamespace parts.[i])
            collector.Add(Literals.dot)
        collector.Add(tagClass parts.[parts.Length - 1])

    type XmlDocReader private (doc: XElement) = 

        let tryFindParameter name = 
            doc.Descendants (XName.op_Implicit "param")
            |> Seq.tryFind (fun el -> 
                match el.Attribute(XName.op_Implicit "name") with
                | null -> false
                | attr -> attr.Value = name)

        static member TryCreate (xml: string) =
            try Some (XmlDocReader(XElement.Parse(ProcessXml xml))) with _ -> None

        member __.CollectSummary(collector: ITaggedTextCollector) = 
            match Seq.tryHead (doc.Descendants(XName.op_Implicit "summary")) with
            | None -> ()
            | Some el ->
                EnsureHardLine collector
                WriteElement collector el

        member this.CollectParameter(collector: ITaggedTextCollector, paramName: string) =
            match tryFindParameter paramName with
            | None -> ()
            | Some el ->
                EnsureHardLine collector
                WriteNodes collector (el.Nodes())
           
        member this.CollectParameters(collector: ITaggedTextCollector) =
            for p in doc.Descendants(XName.op_Implicit "param") do
                match p.Attribute(XName.op_Implicit "name") with
                | null -> ()
                | name ->
                    EnsureHardLine collector
                    collector.Add(tagParameter name.Value)
                    collector.Add(Literals.colon)
                    collector.Add(Literals.space)
                    WriteNodes collector (p.Nodes())

        member this.CollectExceptions(collector: ITaggedTextCollector) =
            let mutable started = false;
            for p in doc.Descendants(XName.op_Implicit "exception") do
                match p.Attribute(XName.op_Implicit "cref") with
                | null -> ()
                | exnType ->
                    if not started then
                        started <- true
                        AppendHardLine collector
                        AppendOnNewLine collector (SR.ExceptionsHeader())
                    EnsureHardLine collector
                    collector.Add(tagSpace "    ")
                    WriteTypeName collector exnType.Value
                    if not (Seq.isEmpty (p.Nodes())) then
                        collector.Add Literals.space
                        collector.Add Literals.minus
                        collector.Add Literals.space
                        WriteNodes collector (p.Nodes())

    type VsThreadToken() = class end
    let vsToken = VsThreadToken()

    type FSharpXmlDocumentationProvider(assemblyPath) =
        inherit Microsoft.CodeAnalysis.XmlDocumentationProvider()
        let xmlPath = Path.ChangeExtension(assemblyPath, ".xml")
        let xmlExists = File.Exists xmlPath
        member x.XmlPath = xmlPath
        member x.GetDocumentation documentationCommentId =
            match xmlExists with
            | true ->
                let xml = base.GetDocumentationForSymbol(documentationCommentId, Globalization.CultureInfo.CurrentCulture, CancellationToken.None)
                xml
            | false -> "<root></root>"

        override x.GetSourceStream(_cancellationToken) =
            new FileStream(xmlPath, FileMode.Open, FileAccess.Read) :> Stream

        override x.Equals(obj) =
            match obj with
            | :? FSharpXmlDocumentationProvider as provider ->
                provider.XmlPath = xmlPath
            | _ ->
                false

        override x.GetHashCode() = xmlPath.GetHashCode()

    /// Provide Xml Documentation             
    type Provider() = 
        let AppendMemberData(xmlCollector: ITaggedTextCollector, exnCollector: ITaggedTextCollector, xmlDocReader: XmlDocReader, showExceptions, showParameters) =
            AppendHardLine xmlCollector
            xmlCollector.StartXMLDoc()
            xmlDocReader.CollectSummary(xmlCollector)

            if (showParameters) then
                xmlDocReader.CollectParameters xmlCollector
            if (showExceptions) then 
                xmlDocReader.CollectExceptions exnCollector

        interface IDocumentationBuilder with 
            /// Append the given processed XML formatted into the string builder
            override __.AppendDocumentationFromProcessedXML(xmlCollector, exnCollector, processedXml, showExceptions, showParameters, paramName) =
                match XmlDocReader.TryCreate processedXml with
                | Some xmlDocReader ->
                    match paramName with
                    | Some paramName -> xmlDocReader.CollectParameter(xmlCollector, paramName)
                    | None -> AppendMemberData(xmlCollector, exnCollector, xmlDocReader, showExceptions,showParameters)
                | None -> ()

            /// Append Xml documentation contents into the StringBuilder
            override this.AppendDocumentation
                            ( /// ITaggedTextCollector to add to
                              xmlCollector: ITaggedTextCollector,
                              /// ITaggedTextCollector to add to
                              exnCollector: ITaggedTextCollector,
                              /// Name of the library file
                              filename:string,
                              /// Signature of the comment
                              signature:string,
                              /// Whether to show exceptions
                              showExceptions:bool,
                              /// Whether to show parameters and return
                              showParameters:bool,
                              /// Name of parameter
                              paramName:string option                            
                             ) = 
                try     
                    (this:>IDocumentationBuilder).AppendDocumentationFromProcessedXML(xmlCollector, exnCollector, FSharpXmlDocumentationProvider(filename).GetDocumentation(signature), showExceptions, showParameters, paramName)
                with e-> 
                    Assert.Exception(e)
                    reraise()    
 
    /// Append an XmlCommnet to the segment.
    let AppendXmlComment(documentationProvider:IDocumentationBuilder, xmlCollector: ITaggedTextCollector, exnCollector: ITaggedTextCollector, xml, showExceptions, showParameters, paramName) =
        match xml with
        | FSharpXmlDoc.None -> ()
        | FSharpXmlDoc.XmlDocFileSignature(filename,signature) ->
            documentationProvider.AppendDocumentation(xmlCollector, exnCollector, filename, signature, showExceptions, showParameters, paramName)
        | FSharpXmlDoc.Text(rawXml) ->
            let processedXml = ProcessXml(rawXml)
            documentationProvider.AppendDocumentationFromProcessedXML(xmlCollector, exnCollector, processedXml, showExceptions, showParameters, paramName)

    let private AddSeparator (collector: ITaggedTextCollector) =
        if not collector.IsEmpty then
            EnsureHardLine collector
            collector.Add (tagText "-------------")
            AppendHardLine collector

    /// Build a data tip text string with xml comments injected.
    let BuildTipText(documentationProvider:IDocumentationBuilder, 
                     dataTipText: FSharpStructuredToolTipElement list,
                     textCollector, xmlCollector,  typeParameterMapCollector, usageCollector, exnCollector,
                     showText, showExceptions, showParameters) = 
        let textCollector: ITaggedTextCollector = TextSanitizingCollector(textCollector, lineLimit = 45) :> _
        let xmlCollector: ITaggedTextCollector = TextSanitizingCollector(xmlCollector, lineLimit = 45) :> _
        let typeParameterMapCollector: ITaggedTextCollector = TextSanitizingCollector(typeParameterMapCollector, lineLimit = 6) :> _
        let exnCollector: ITaggedTextCollector = TextSanitizingCollector(exnCollector, lineLimit = 45) :> _
        let usageCollector: ITaggedTextCollector = TextSanitizingCollector(usageCollector, lineLimit = 45) :> _

        let addSeparatorIfNecessary add =
            if add then
                AddSeparator textCollector
                AddSeparator xmlCollector

        let ProcessGenericParameters (tps: Layout list) =
            if not tps.IsEmpty then
                AppendHardLine typeParameterMapCollector
                AppendOnNewLine typeParameterMapCollector (SR.GenericParametersHeader())
                for tp in tps do 
                    AppendHardLine typeParameterMapCollector
                    typeParameterMapCollector.Add(tagSpace "    ")
                    renderL (taggedTextListR typeParameterMapCollector.Add) tp |> ignore

        let Process add (dataTipElement: FSharpStructuredToolTipElement) =

            match dataTipElement with 
            | FSharpStructuredToolTipElement.None -> 
                false

            | FSharpStructuredToolTipElement.Group (overloads) -> 
                let overloads = Array.ofList overloads
                let len = overloads.Length
                if len >= 1 then
                    addSeparatorIfNecessary add
                    if showText then 
                        let AppendOverload (item: FSharpToolTipElementData<_>) = 
                            if not(isEmptyL item.MainDescription) then
                                if not textCollector.IsEmpty then 
                                    AppendHardLine textCollector
                                renderL (taggedTextListR textCollector.Add) item.MainDescription |> ignore

                        AppendOverload(overloads.[0])
                        if len >= 2 then AppendOverload(overloads.[1])
                        if len >= 3 then AppendOverload(overloads.[2])
                        if len >= 4 then AppendOverload(overloads.[3])
                        if len >= 5 then AppendOverload(overloads.[4])
                        if len >= 6 then 
                            AppendHardLine textCollector
                            textCollector.Add (tagText(PrettyNaming.FormatAndOtherOverloadsString(len-5)))

                    let item0 = overloads.[0]

                    item0.Remarks |> Option.iter (fun r -> 
                        if not(isEmptyL r) then
                            AppendHardLine usageCollector
                            renderL (taggedTextListR usageCollector.Add) r |> ignore)

                    AppendXmlComment(documentationProvider, xmlCollector, exnCollector, item0.XmlDoc, showExceptions, showParameters, item0.ParamName)

                    if showText then 
                        ProcessGenericParameters item0.TypeMapping

                    true
                else
                    false

            | FSharpStructuredToolTipElement.CompositionError(errText) -> 
                textCollector.Add(tagText errText)
                true

        List.fold Process false dataTipText |> ignore

    let BuildDataTipText(documentationProvider, textCollector, xmlCollector, typeParameterMapCollector, usageCollector, exnCollector, FSharpToolTipText(dataTipText)) = 
        BuildTipText(documentationProvider, dataTipText, textCollector, xmlCollector, typeParameterMapCollector, usageCollector, exnCollector, true, true, false) 

    let BuildMethodOverloadTipText(documentationProvider, textCollector, xmlCollector, FSharpToolTipText(dataTipText), showParams) = 
        BuildTipText(documentationProvider, dataTipText, textCollector, xmlCollector, xmlCollector, ignore, ignore, false, false, showParams) 

    let BuildMethodParamText(documentationProvider, xmlCollector, xml, paramName) =
        AppendXmlComment(documentationProvider, TextSanitizingCollector(xmlCollector), TextSanitizingCollector(xmlCollector), xml, false, true, Some paramName)

    //let documentationBuilderCache = ConditionalWeakTable<IVsXMLMemberIndexService, IDocumentationBuilder>()
    let CreateDocumentationBuilder((*xmlIndexService: IVsXMLMemberIndexService*)) = 
        //documentationBuilderCache.GetValue(xmlIndexService,(fun _ -> Provider((*xmlIndexService*)) :> IDocumentationBuilder))
        Provider((*xmlIndexService*)) :> IDocumentationBuilder