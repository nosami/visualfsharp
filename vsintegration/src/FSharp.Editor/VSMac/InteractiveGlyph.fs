//
// InteractiveGlyph.fs
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

open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Core.Imaging
open System.ComponentModel.Composition
open Microsoft.VisualStudio.Text.Tagging
open System
open System.Collections.Generic
open Microsoft.VisualStudio.Text
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor

type InteractivePromptGlyphTag() = interface IGlyphTag

type InteractiveGlyphFactory(imageId:ImageId, imageService:IImageService) =
    let mutable imageCache: AppKit.NSImage option = None
    
    interface IGlyphFactory with
        member x.GenerateGlyph(line, tag) =
            match tag with
            | :? InteractivePromptGlyphTag ->
                if imageCache.IsNone then
                    imageCache <- Some(imageService.GetImage (imageId) :?> AppKit.NSImage)
                let imageView = AppKit.NSImageView.FromImage imageCache.Value
                imageView.SetFrameSize (imageView.FittingSize)
                Some (box imageView)
            | _ -> None
            |> Option.toObj

[<Export(typeof<IGlyphFactoryProvider>)>]
[<Microsoft.VisualStudio.Utilities.Name("InteractivePromptGlyphTag")>]
[<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpInteractiveContentType)>]
[<TagType(typeof<InteractivePromptGlyphTag>)>]
type InteractiveGlyphFactoryProvider() as this =
    [<Import>]
    member val ImageService:IImageService = null with get, set

    interface IGlyphFactoryProvider with
        member x.GetGlyphFactory(view, margin) =
            let imageId = ImageId(Guid("{3404e281-57a6-4f3a-972b-185a683e0753}"), 1)
            upcast InteractiveGlyphFactory(imageId, x.ImageService)

type InteractivePromptGlyphTagger(textView: ITextView) as this =
    let tagsChanged = Event<_,_>()

    let promptSpans = HashSet<_>()

    let getLastLine() =
         let snapshot = textView.TextBuffer.CurrentSnapshot
         let lineCount = snapshot.LineCount

         if lineCount > 0 then
             Some (snapshot.GetLineFromLineNumber(lineCount - 1))
         else
             None

    let isOnLastLine(pos:int) =
        match getLastLine() with
        | Some line -> line.Start.Position = pos
        | None -> false

    do
        textView.Properties.[typeof<InteractivePromptGlyphTagger>] <- this

    member x.AddPrompt(pos:int) =
        if promptSpans.Add(pos) then
            tagsChanged.Trigger(this, SnapshotSpanEventArgs(SnapshotSpan(textView.TextSnapshot, pos, 1)))
    
    interface ITagger<InteractivePromptGlyphTag> with
        [<CLIEvent>]
        member this.TagsChanged = tagsChanged.Publish
        
        member x.GetTags(spans) =
            seq {
                for span in spans do
                    if promptSpans.Contains(span.Start.Position) || isOnLastLine(span.Start.Position) then
                        yield TagSpan<InteractivePromptGlyphTag>(span, InteractivePromptGlyphTag())
            }

module InteractiveGlyphManagerService =
    let interactiveGlyphTagger(textView: ITextView) =
        textView.Properties.GetOrCreateSingletonProperty(typeof<InteractivePromptGlyphTagger>, fun () -> InteractivePromptGlyphTagger textView)
