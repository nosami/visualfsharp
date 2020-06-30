namespace Microsoft.VisualStudio.FSharp.Editor.Logging

open System
open Microsoft.VisualStudio.FSharp.Editor
open MonoDevelop.Core

[<RequireQualifiedAccess>]
type LogType =
    | Info
    | Warn
    | Error
    | Message
    override x.ToString () =
        match x with
        | Message   -> "Message"
        | Info      -> "Information"
        | Warn      -> "Warning"
        | Error     -> "Error"

[<AutoOpen>]
module Logging =
    let inline private log f = Printf.kprintf f

    let inline private logWithThread f format =
        log (log f "[UI - %b] %s" Runtime.IsMainThread) format

    let logDebug format = logWithThread LoggingService.LogDebug format
    let logErrorf format = logWithThread LoggingService.LogError format
    let logInfof format = logWithThread LoggingService.LogInfo format
    let logWarning format = logWithThread LoggingService.LogWarning format

    let logException (ex: Exception) =
        logErrorf "Exception Message: %s\nStack Trace: %s" ex.Message ex.StackTrace

    let logExceptionWithContext(ex: Exception, context) =
        logErrorf "Context: %s\nException Message: %s\nStack Trace: %s" context ex.Message ex.StackTrace