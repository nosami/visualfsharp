// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
namespace Microsoft.CodeAnalysis.ExternalAccess.FSharp.Internal

open System.ComponentModel.Composition
open Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor

type FSharpContentTypeDefinitions() =
    [<Export>]
    [<Microsoft.VisualStudio.Utilities.Name(FSharpContentTypeNames.FSharpSignatureHelpContentType)>]
    [<Microsoft.VisualStudio.Utilities.BaseDefinition("sighelp")>]
    member val FSharpSignatureHelpContentTypeDefinition: Microsoft.VisualStudio.Utilities.ContentTypeDefinition = null with get, set

    [<Export>]
    [<Microsoft.VisualStudio.Utilities.FileExtension(".fs;.fsx;.fsi")>]
    [<Microsoft.VisualStudio.Utilities.ContentType(FSharpContentTypeNames.FSharpContentType)>]
    member val FSharpFileExtension: Microsoft.VisualStudio.Utilities.FileExtensionToContentTypeDefinition = null with get, set