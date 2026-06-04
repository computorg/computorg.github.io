namespace QuartoInspect

open System
open System.Diagnostics
open System.IO
open System.Text.Json

/// Client for running Quarto inspect commands
module QuartoClient =

    /// Result of running quarto inspect
    type InspectResult =
        { jsonContent: string
          exitCode: int
          stderr: string
          stdout: string
          executionTime: TimeSpan }

    /// Utility function for erroring with about missing fields
    let errorMissingFields (has: string -> bool) (fields: string list) =
        let missingFields = fields |> List.filter (fun f -> not (has f))

        if missingFields.Length > 0 then
            let missingFieldNames = missingFields |> String.concat ", "
            Result.Error $"Missing required fields: {missingFieldNames}"
        else
            Ok()

    /// Run quarto inspect on a given path
    let runInspect (path: string) : Async<Result<InspectResult, string>> =
        async {
            try
                let startTime = DateTime.Now
                let tempDir = Path.GetTempPath()
                let outputFile = Path.Combine(tempDir, $"quarto-inspect-{Guid.NewGuid()}.json")

                let processInfo = ProcessStartInfo()
                processInfo.FileName <- "quarto"
                processInfo.Arguments <- $"inspect \"{path}\" \"{outputFile}\""
                processInfo.RedirectStandardOutput <- true
                processInfo.RedirectStandardError <- true
                processInfo.UseShellExecute <- false
                processInfo.CreateNoWindow <- true

                use proc = Process.Start(processInfo)
                do! Async.Sleep(100) // Give process time to start

                let! _ = proc.WaitForExitAsync() |> Async.AwaitTask

                let stdout = proc.StandardOutput.ReadToEnd()
                let stderr = proc.StandardError.ReadToEnd()
                let executionTime = DateTime.Now - startTime

                if proc.ExitCode <> 0 then
                    return Error $"quarto inspect failed (exit code {proc.ExitCode}): {stderr}"
                else if not (File.Exists(outputFile)) then
                    return Error $"quarto inspect output file not found at {outputFile}"
                else
                    let jsonContent = File.ReadAllText(outputFile)

                    try
                        File.Delete(outputFile)
                    with _ ->
                        ()

                    return
                        Ok
                            { jsonContent = jsonContent
                              exitCode = proc.ExitCode
                              stderr = stderr
                              stdout = stdout
                              executionTime = executionTime }
            with ex ->
                return Error $"Error running quarto inspect: {ex.Message}"
        }

    /// Validate JSON against document schema
    let validateDocumentSchema (jsonStr: string) : Result<JsonElement, string> =
        try
            let doc = JsonDocument.Parse(jsonStr)
            let root = doc.RootElement

            let has (key: string) =
                let mutable elem = Unchecked.defaultof<JsonElement>
                root.TryGetProperty(key, &elem)
            // Check required fields
            [ "quarto"; "engines"; "formats" ]
            |> errorMissingFields has
            |> Result.bind (fun () -> Ok root)
        with ex ->
            Error $"Invalid JSON: {ex.Message}"

    /// Validate JSON against project schema
    let validateProjectSchema (jsonStr: string) : Result<JsonElement, string> =
        try
            let doc = JsonDocument.Parse(jsonStr)
            let root = doc.RootElement

            let has (key: string) =
                let mutable elem = Unchecked.defaultof<JsonElement>
                root.TryGetProperty(key, &elem)
            // Check required fields
            [ "quarto"; "dir"; "engines"; "files" ]
            |> errorMissingFields has
            |> Result.bind (fun () -> Ok root)
        with ex ->
            Error $"Invalid JSON: {ex.Message}"

    /// Check if Quarto is installed and accessible
    let checkQuartoAvailable () : Async<Result<string, string>> =
        async {
            try
                let processInfo = ProcessStartInfo()
                processInfo.FileName <- "quarto"
                processInfo.Arguments <- "--version"
                processInfo.RedirectStandardOutput <- true
                processInfo.RedirectStandardError <- true
                processInfo.UseShellExecute <- false
                processInfo.CreateNoWindow <- true

                use proc = Process.Start(processInfo)
                let! _ = proc.WaitForExitAsync() |> Async.AwaitTask

                if proc.ExitCode = 0 then
                    let version = proc.StandardOutput.ReadToEnd().Trim()
                    return Ok version
                else
                    let error = proc.StandardError.ReadToEnd()
                    return Error $"Quarto version check failed: {error}"
            with ex ->
                return Error $"Quarto not available: {ex.Message}"
        }
