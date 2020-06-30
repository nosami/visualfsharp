namespace MonoDevelop.FSharp
open FSharp.Compiler.AbstractIL.Internal.Library
open System.IO
open MonoDevelop.Ide
open MonoDevelop.Ide.Gui
open MonoDevelop.Core
type Version = int

type FileSystem (defaultFileSystem : IFileSystem, openDocuments: unit -> Document seq) =
    static member IsAScript fileName =
        let ext = Path.GetExtension fileName
        [".fsx";".fsscript";".sketchfs"] |> List.exists ((=) ext)

module FileService =
    let supportedFileExtensions =
        set [".fsscript"; ".fs"; ".fsx"; ".fsi"; ".sketchfs"]
    
    /// Is the specified extension supported F# file?
    let supportedFileName fileName =
        if fileName = null then
            false
        else
            let ext = Path.GetExtension(fileName).ToLower()
            supportedFileExtensions
            |> Set.contains ext
    
    let isInsideFSharpFile () =
        if IdeApp.Workbench.ActiveDocument = null ||
            IdeApp.Workbench.ActiveDocument.FileName.FileName = null then false
        else
            let file = IdeApp.Workbench.ActiveDocument.FileName.ToString()
            supportedFileName (file)
    
    let supportedFilePath (filePath:FilePath) =
        supportedFileName (string filePath)

