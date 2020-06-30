// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open Microsoft.VisualStudio.Text.Classification
open System.ComponentModel.Composition
open System
open System.Windows.Media
open Microsoft.VisualStudio.Language.StandardClassification

module internal ClassificationDefinition =
    [<Export>]
    [<Microsoft.VisualStudio.Utilities.Name(FSharpClassificationTypes.MutableVar)>]
    [<Microsoft.VisualStudio.Utilities.BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)>]
    let FSharpMutableVarClassificationType : ClassificationTypeDefinition = null
    
[<Export(typeof<EditorFormatDefinition>)>]
[<ClassificationType(ClassificationTypeNames = FSharpClassificationTypes.MutableVar)>]
[<Microsoft.VisualStudio.Utilities.Name(FSharpClassificationTypes.MutableVar)>]
[<UserVisible(true)>]
[<Microsoft.VisualStudio.Utilities.Order(After = "keyword")>]
type internal FSharpMutableVarTypeFormat() as self =
    inherit EditorFormatDefinition()

    do self.DisplayName <- SR.FSharpMutableVarsClassificationType()
       self.ForegroundColor <- Nullable(Color.FromRgb(255uy, 210uy, 28uy))