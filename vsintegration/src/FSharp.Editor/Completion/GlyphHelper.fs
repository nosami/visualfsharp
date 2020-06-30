// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor
open System
open Microsoft.CodeAnalysis.Tags;
open Microsoft.VisualStudio.Core.Imaging;
open Microsoft.VisualStudio.Imaging;
module GlyphHelper =
    // hardcode imageCatalogGuid locally rather than calling KnownImageIds.ImageCatalogGuid
    // So it does not have dependency on Microsoft.VisualStudio.ImageCatalog.dll
    // https://github.com/dotnet/roslyn/issues/26642
    let imageCatalogGuid = Guid.Parse("ae27a6b0-e345-4288-96df-5eaf394ee369");

    let getImageId (glyph:Microsoft.CodeAnalysis.ExternalAccess.FSharp.FSharpGlyph) =
      // VS for mac cannot refer to ImageMoniker
      // so we need to expose ImageId instead of ImageMoniker here
      // and expose ImageMoniker in the EditorFeatures.wpf.dll
        match glyph with
        | Glyph.None ->
            new ImageId(imageCatalogGuid, KnownImageIds.None)

        | Glyph.Assembly ->
            new ImageId(imageCatalogGuid, KnownImageIds.Assembly)

        | Glyph.BasicFile ->
            new ImageId(imageCatalogGuid, KnownImageIds.VBFileNode)
        | Glyph.BasicProject ->
            new ImageId(imageCatalogGuid, KnownImageIds.VBProjectNode)

        | Glyph.ClassPublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.ClassPublic)
        | Glyph.ClassProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.ClassProtected)
        | Glyph.ClassPrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.ClassPrivate)
        | Glyph.ClassInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.ClassInternal)

        | Glyph.CSharpFile ->
            new ImageId(imageCatalogGuid, KnownImageIds.CSFileNode)
        | Glyph.CSharpProject ->
            new ImageId(imageCatalogGuid, KnownImageIds.CSProjectNode)

        | Glyph.ConstantPublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.ConstantPublic)
        | Glyph.ConstantProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.ConstantProtected)
        | Glyph.ConstantPrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.ConstantPrivate)
        | Glyph.ConstantInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.ConstantInternal)

        | Glyph.DelegatePublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.DelegatePublic)
        | Glyph.DelegateProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.DelegateProtected)
        | Glyph.DelegatePrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.DelegatePrivate)
        | Glyph.DelegateInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.DelegateInternal)

        | Glyph.EnumPublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.EnumerationPublic)
        | Glyph.EnumProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.EnumerationProtected)
        | Glyph.EnumPrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.EnumerationPrivate)
        | Glyph.EnumInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.EnumerationInternal)

        | Glyph.EnumMemberPublic
        | Glyph.EnumMemberProtected
        | Glyph.EnumMemberPrivate
        | Glyph.EnumMemberInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.EnumerationItemPublic)

        | Glyph.Error ->
            new ImageId(imageCatalogGuid, KnownImageIds.StatusError)

        | Glyph.EventPublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.EventPublic)
        | Glyph.EventProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.EventProtected)
        | Glyph.EventPrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.EventPrivate)
        | Glyph.EventInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.EventInternal)

        // Extension methods have the same glyph regardless of accessibility.
        | Glyph.ExtensionMethodPublic
        | Glyph.ExtensionMethodProtected
        | Glyph.ExtensionMethodPrivate
        | Glyph.ExtensionMethodInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.ExtensionMethod);

        | Glyph.FieldPublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.FieldPublic)
        | Glyph.FieldProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.FieldProtected)
        | Glyph.FieldPrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.FieldPrivate)
        | Glyph.FieldInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.FieldInternal)

        | Glyph.InterfacePublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.InterfacePublic)
        | Glyph.InterfaceProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.InterfaceProtected)
        | Glyph.InterfacePrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.InterfacePrivate)
        | Glyph.InterfaceInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.InterfaceInternal)

        // TODO: Figure out the right thing to return here.
        | Glyph.Intrinsic ->
            new ImageId(imageCatalogGuid, KnownImageIds.Type)

        | Glyph.Keyword ->
            new ImageId(imageCatalogGuid, KnownImageIds.IntellisenseKeyword)

        | Glyph.Label ->
            new ImageId(imageCatalogGuid, KnownImageIds.Label)

        | Glyph.Parameter
        | Glyph.Local ->
            new ImageId(imageCatalogGuid, KnownImageIds.LocalVariable);

        | Glyph.Namespace ->
            new ImageId(imageCatalogGuid, KnownImageIds.Namespace)

        | Glyph.MethodPublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.MethodPublic)
        | Glyph.MethodProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.MethodProtected)
        | Glyph.MethodPrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.MethodPrivate)
        | Glyph.MethodInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.MethodInternal)

        | Glyph.ModulePublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.ModulePublic)
        | Glyph.ModuleProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.ModuleProtected)
        | Glyph.ModulePrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.ModulePrivate)
        | Glyph.ModuleInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.ModuleInternal)

        | Glyph.OpenFolder ->
            new ImageId(imageCatalogGuid, KnownImageIds.OpenFolder)

        | Glyph.Operator ->
            new ImageId(imageCatalogGuid, KnownImageIds.Operator)

        | Glyph.PropertyPublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.PropertyPublic)
        | Glyph.PropertyProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.PropertyProtected)
        | Glyph.PropertyPrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.PropertyPrivate)
        | Glyph.PropertyInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.PropertyInternal)

        | Glyph.RangeVariable ->
            new ImageId(imageCatalogGuid, KnownImageIds.FieldPublic)

        | Glyph.Reference ->
            new ImageId(imageCatalogGuid, KnownImageIds.Reference)

        //// this is not a copy-paste mistake, we were using these before in the previous GetImageMoniker()
        //| Glyph.StructurePublic ->
        // return KnownMonikers.ValueTypePublic
        //| Glyph.StructureProtected ->
        // return KnownMonikers.ValueTypeProtected
        //| Glyph.StructurePrivate ->
        // return KnownMonikers.ValueTypePrivate
        //| Glyph.StructureInternal ->
        // return KnownMonikers.ValueTypeInternal

        | Glyph.StructurePublic ->
            new ImageId(imageCatalogGuid, KnownImageIds.ValueTypePublic)
        | Glyph.StructureProtected ->
            new ImageId(imageCatalogGuid, KnownImageIds.ValueTypeProtected)
        | Glyph.StructurePrivate ->
            new ImageId(imageCatalogGuid, KnownImageIds.ValueTypePrivate)
        | Glyph.StructureInternal ->
            new ImageId(imageCatalogGuid, KnownImageIds.ValueTypeInternal)

        | Glyph.TypeParameter ->
            new ImageId(imageCatalogGuid, KnownImageIds.Type)

        | Glyph.Snippet ->
            new ImageId(imageCatalogGuid, KnownImageIds.Snippet)

        | Glyph.CompletionWarning ->
            new ImageId(imageCatalogGuid, KnownImageIds.IntellisenseWarning)

        | Glyph.StatusInformation ->
            new ImageId(imageCatalogGuid, KnownImageIds.StatusInformation)

        | Glyph.NuGet ->
            new ImageId(imageCatalogGuid, KnownImageIds.NuGet)

        //| Glyph.TargetTypeMatch ->
            //new ImageId(imageCatalogGuid, KnownImageIds.MatchType)

        | _ ->
            raise(new ArgumentException("glyph"))
