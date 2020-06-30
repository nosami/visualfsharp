

namespace FSharp.Editor
open System
open System.Reflection

[<System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0");
  System.Diagnostics.DebuggerNonUserCodeAttribute;
  System.Runtime.CompilerServices.CompilerGeneratedAttribute>]
type FSharp_Editor() = 
        [<DefaultValue(false)>]
        static val mutable private resourceMan:System.Resources.ResourceManager
        
        [<DefaultValue(false)>]
        static val mutable private resourceCulture:System.Globalization.CultureInfo
        [<System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)>]
        member this.ResourceManager
            with get() : System.Resources.ResourceManager =
                if System.Object.Equals((Unchecked.defaultof<_>), FSharp_Editor.resourceMan) then
                    let mutable (temp:System.Resources.ResourceManager) = new System.Resources.ResourceManager("FSharp.Editor.resx", (typeof<FSharp_Editor>).Assembly)
                    FSharp_Editor.resourceMan <- temp
                ((FSharp_Editor.resourceMan :> obj) :?> System.Resources.ResourceManager)
        
        [<System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)>]
        member this.Culture
            with get() : System.Globalization.CultureInfo =
                ((FSharp_Editor.resourceCulture :> obj) :?> System.Globalization.CultureInfo)
            and set(value:System.Globalization.CultureInfo) : unit =
                FSharp_Editor.resourceCulture <- value
        
        member this.AddNewKeyword
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("AddNewKeyword", FSharp_Editor.resourceCulture)
        
        member this.ImplementInterface
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("ImplementInterface", FSharp_Editor.resourceCulture)
        
        member this.ImplementInterfaceWithoutTypeAnnotation
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("ImplementInterfaceWithoutTypeAnnotation", FSharp_Editor.resourceCulture)
        
        member this.PrefixValueNameWithUnderscore
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("PrefixValueNameWithUnderscore", FSharp_Editor.resourceCulture)
        
        member this.RenameValueToUnderscore
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("RenameValueToUnderscore", FSharp_Editor.resourceCulture)
        
        member this.SimplifyName
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("SimplifyName", FSharp_Editor.resourceCulture)
        
        member this.NameCanBeSimplified
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("NameCanBeSimplified", FSharp_Editor.resourceCulture)
        
        member this.FSharpFunctionsOrMethodsClassificationType
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("FSharpFunctionsOrMethodsClassificationType", FSharp_Editor.resourceCulture)
        
        member this.FSharpMutableVarsClassificationType
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("FSharpMutableVarsClassificationType", FSharp_Editor.resourceCulture)
        
        member this.FSharpPrintfFormatClassificationType
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("FSharpPrintfFormatClassificationType", FSharp_Editor.resourceCulture)
        
        member this.FSharpPropertiesClassificationType
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("FSharpPropertiesClassificationType", FSharp_Editor.resourceCulture)
        
        member this.FSharpDisposablesClassificationType
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("FSharpDisposablesClassificationType", FSharp_Editor.resourceCulture)
        
        member this.RemoveUnusedOpens
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("RemoveUnusedOpens", FSharp_Editor.resourceCulture)
        
        member this.UnusedOpens
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("UnusedOpens", FSharp_Editor.resourceCulture)
        
        //member this.6008
        //    with get() : string =
        //        FSharp_Editor.ResourceManager.GetString("6008", FSharp_Editor.resourceCulture)
        
        //member this.6009
            //with get() : string =
                //FSharp_Editor.ResourceManager.GetString("6009", FSharp_Editor.resourceCulture)
        
        member this.AddAssemblyReference
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("AddAssemblyReference", FSharp_Editor.resourceCulture)
        
        member this.AddProjectReference
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("AddProjectReference", FSharp_Editor.resourceCulture)
        
        //member this.6010
        //    with get() : string =
        //        FSharp_Editor.ResourceManager.GetString("6010", FSharp_Editor.resourceCulture)
        
        //member this.6011
        //    with get() : string =
        //        FSharp_Editor.ResourceManager.GetString("6011", FSharp_Editor.resourceCulture)
        
        //member this.6012
        //    with get() : string =
        //        FSharp_Editor.ResourceManager.GetString("6012", FSharp_Editor.resourceCulture)
        
        //member this.6013
        //    with get() : string =
        //        FSharp_Editor.ResourceManager.GetString("6013", FSharp_Editor.resourceCulture)
        
        //member this.6014
            //with get() : string =
                //FSharp_Editor.ResourceManager.GetString("6014", FSharp_Editor.resourceCulture)
        
        member this.TheValueIsUnused
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("TheValueIsUnused", FSharp_Editor.resourceCulture)
        
        member this.CannotDetermineSymbol
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("CannotDetermineSymbol", FSharp_Editor.resourceCulture)
        
        member this.CannotNavigateUnknown
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("CannotNavigateUnknown", FSharp_Editor.resourceCulture)
        
        member this.LocatingSymbol
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("LocatingSymbol", FSharp_Editor.resourceCulture)
        
        member this.NavigatingTo
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("NavigatingTo", FSharp_Editor.resourceCulture)
        
        member this.NavigateToFailed
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("NavigateToFailed", FSharp_Editor.resourceCulture)
        
        member this.ExceptionsHeader
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("ExceptionsHeader", FSharp_Editor.resourceCulture)
        
        member this.GenericParametersHeader
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("GenericParametersHeader", FSharp_Editor.resourceCulture)
        
        member this.RenameValueToDoubleUnderscore
            with get() : string =
                FSharp_Editor.ResourceManager.GetString("RenameValueToDoubleUnderscore", FSharp_Editor.resourceCulture)