namespace FSharp.Editor

open System
open System.IO
open System.Diagnostics
open System.Runtime.Serialization.Formatters.Binary
open MonoDevelop.Core
open MonoDevelop.FSharp
open Newtonsoft.Json
open Microsoft.VisualStudio.FSharp.Editor.Extensions
open Newtonsoft.Json.Converters
open FSharp.Compiler.SourceCodeServices

type CompletionData = {
    displayText: string
    completionText: string
    category: string
    [<JsonConverter(typeof<StringEnumConverter>)>]
    icon: Microsoft.CodeAnalysis.ExternalAccess.FSharp.FSharpGlyph
    overloads: CompletionData array
    description: string
}

module binaryDeserializer =
    let  deserializeFromString<'T>(base64) =
        match base64 with
        | "" ->
            None
        | _ ->
            let b = Convert.FromBase64String(base64)
            use stream = new MemoryStream(b)
            let formatter = new BinaryFormatter()
            let (o:'T) = downcast formatter.Deserialize(stream)
            Some o

type InteractiveSession(pathToExe) =
    let (|Completion|_|) (command: string) =
        if command.StartsWith("completion ") then
            let payload = command.[11..]
            Some (JsonConvert.DeserializeObject<CompletionData array> payload)
        else
            None

    let (|Tooltip|_|) (command: string) =
        if command.StartsWith("tooltip ") then
            let payload = command.[8..]
            Some (binaryDeserializer.deserializeFromString<FSharpStructuredToolTipText> payload)
        else
            None

    let (|ParameterHints|_|) (command: string) =
        if command.StartsWith("parameter-hints ") then
            let payload = command.[16..]
            Some (binaryDeserializer.deserializeFromString<(FSharpNoteworthyParamInfoLocations * FSharpMethodGroup)> payload)
        else
            None

    let (|Image|_|) (command: string) =
        if command.StartsWith("image ") then
            let base64image = command.[6..command.Length - 1]
            let bytes = Convert.FromBase64String base64image
            use ms = new MemoryStream(bytes)
            Some (Xwt.Drawing.Image.FromStream ms)
        else
            None

    let (|ServerPrompt|_|) (command:string) =
        if command = "SERVER-PROMPT>" then
            Some ()
        else
            None

    let textReceived = Event<_>()
    let promptReady = Event<_>()        

    let completionsReceivedEvent = new Event<CompletionData array>()
    let imageReceivedEvent = new Event<Xwt.Drawing.Image>()
    let tooltipReceivedEvent = new Event<FSharpStructuredToolTipText option>()
    let parameterHintReceivedEvent = new Event<(FSharpNoteworthyParamInfoLocations * FSharpMethodGroup) option>()

    let mutable hasStarted = false
    let startProcess() =
        let processPid = sprintf " %d" (Process.GetCurrentProcess().Id)

        let processName = 
            if Environment.runningOnMono then Environment.getMonoPath() else pathToExe

        let arguments = 
            if Environment.runningOnMono then pathToExe + processPid else processPid

        let startInfo =
            new ProcessStartInfo
              (FileName = processName, UseShellExecute = false, Arguments = arguments,
              RedirectStandardError = true, CreateNoWindow = true, RedirectStandardOutput = true,
              RedirectStandardInput = true, StandardErrorEncoding = Text.Encoding.UTF8, StandardOutputEncoding = Text.Encoding.UTF8)

        try
            let proc = Process.Start(startInfo)
            LoggingService.logDebug "Process started %d" proc.Id
            proc.BeginOutputReadLine()
            proc.BeginErrorReadLine()

            proc.OutputDataReceived
            |> Event.filter (fun de -> de.Data <> null)
            |> Event.add (fun de ->
                LoggingService.logDebug "Interactive: received %s" de.Data
                Console.WriteLine de.Data
                match de.Data with
                | Image image -> imageReceivedEvent.Trigger image
                | ServerPrompt -> promptReady.Trigger()
                | data ->
                    if data.Trim() <> "" then
                        textReceived.Trigger(data + "\n"))

            proc.ErrorDataReceived.Subscribe(fun de -> 
                if not (String.IsNullOrEmpty de.Data) then
                    try
                        match de.Data with
                        | Completion completions ->
                            completionsReceivedEvent.Trigger completions
                        | Tooltip tooltip ->
                            tooltipReceivedEvent.Trigger tooltip
                        | ParameterHints hints ->
                            parameterHintReceivedEvent.Trigger hints
                        | _ -> LoggingService.logDebug "[fsharpi] don't know how to process command %s" de.Data

                    with 
                    | :? JsonException as e ->
                        LoggingService.logError "[fsharpi] - error deserializing error stream - %s\\n %s" e.Message de.Data
                        ) |> ignore

            proc.EnableRaisingEvents <- true
            hasStarted <- true
            proc
        with e ->
            LoggingService.logDebug "Interactive: Error %s" (e.ToString())
            reraise()

    let mutable fsiProcess = Unchecked.defaultof<Process>

    let sendCommand(str:string) =
        LoggingService.logDebug "Interactive: sending %s" str
        LoggingService.logDebug "send command %d" fsiProcess.Id

        async {
            let stream = fsiProcess.StandardInput.BaseStream
            let bytes = Text.Encoding.UTF8.GetBytes(str + "\n")
            do! stream.WriteAsync(bytes,0,bytes.Length) |> Async.AwaitTask
            stream.Flush()
        } |> Async.Start

    member x.Interrupt() =
        LoggingService.logDebug "Interactive: Break!"

    member x.CompletionsReceived = completionsReceivedEvent.Publish
    member x.TooltipReceived = tooltipReceivedEvent.Publish
    member x.ParameterHintReceived = parameterHintReceivedEvent.Publish
    member x.ImageReceived = imageReceivedEvent.Publish
    member x.TextReceived = textReceived.Publish
    member x.PromptReady = promptReady.Publish
    member x.StartReceiving() = fsiProcess <- startProcess()

    member x.HasStarted = hasStarted
    member x.HasExited() = fsiProcess.HasExited
    member x.Kill() = fsiProcess.Kill()
    member x.Restart() =
        fsiProcess.Kill()
        fsiProcess <- startProcess()

    member x.SendInput input documentName =
        printfn "%s" input
        documentName
        |> Option.iter(fun fileName ->
            sendCommand (sprintf "input # 0 @\"%s\"" fileName))

        for line in String.getLines input do
            sendCommand ("input " + line)

    member x.SendCompletionRequest input column =
        sendCommand (sprintf "completion %d %s" column input)

    member x.SendParameterHintRequest input column =
        sendCommand (sprintf "parameter-hints %d %s" column input)

    member x.SendTooltipRequest input  =
        sendCommand (sprintf "tooltip %s" input)

    member x.Exited = fsiProcess.Exited
