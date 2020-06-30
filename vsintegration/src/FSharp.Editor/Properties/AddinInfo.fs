namespace Microsoft.VisualStudio.FSharp.Editor
open Mono.Addins

[<Addin ("VisualFSharp", 
  Version = MonoDevelop.BuildInfo.Version,
  Url = "http://github.com/mono/fsharp",
  Category = "Language bindings")>]

[<AddinName ("VisualFSharp")>]
[<AddinDescription ("F# Language Binding (for VSMac " + MonoDevelop.BuildInfo.Version + ").")>]
[<AddinAuthor ("F# Software Foundation (fsharp.org)")>]

[<AddinDependency ("MonoDevelop.Core", MonoDevelop.BuildInfo.Version)>]
[<AddinDependency ("MonoDevelop.Ide", MonoDevelop.BuildInfo.Version)>]
[<AddinDependency ("MonoDevelop.TextEditor", MonoDevelop.BuildInfo.Version)>]
()