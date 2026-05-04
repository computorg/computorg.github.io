namespace QuartoInspect

open FSharp.Data

module QuartoTypes =

    /// Type provider for Quarto project-level inspect output
    [<Literal>]
    let ProjectSamplePath = __SOURCE_DIRECTORY__ + "/sample-project.json"

    type QuartoProjectProvider = JsonProvider<ProjectSamplePath>

    /// Type provider for Quarto document-level inspect output
    [<Literal>]
    let DocumentSamplePath = __SOURCE_DIRECTORY__ + "/sample-document.json"

    type QuartoDocumentProvider = JsonProvider<DocumentSamplePath>

    /// Represents a Quarto version
    type QuartoVersion = { version: string }

    /// Represents a code cell in a document
    type CodeCell =
        { start: int
          ``end``: int
          file: string
          source: string
          language: string
          metadata: Map<string, string> }

    /// Represents an include mapping
    type IncludeMapping = { source: string; target: string }

    /// Represents file-level information
    type FileInfo =
        { includeMap: IncludeMapping list
          codeCells: CodeCell list }

    /// Represents project file information
    type ProjectFiles =
        { input: string list
          resources: string list
          configResources: string list
          config: string list }

    /// Represents a Quarto project inspection result
    type QuartoProjectInfo =
        { quarto: QuartoVersion
          dir: string
          engines: string list
          config: Map<string, string>
          files: ProjectFiles
          fileInformation: Map<string, FileInfo>
          extensions: Map<string, string> list }

    /// Represents a Quarto document inspection result
    type QuartoDocumentInfo =
        { quarto: QuartoVersion
          engines: string list
          formats: Map<string, string>
          resources: string list
          fileInformation: Map<string, FileInfo>
          project: QuartoProjectInfo option }

    /// Parse JSON string to QuartoProjectInfo
    let parseProjectJson (jsonStr: string) : Result<QuartoProjectProvider.Root, string> =
        try
            Ok(QuartoProjectProvider.Parse(jsonStr))
        with ex ->
            Error $"Failed to parse project JSON: {ex.Message}"

    /// Parse JSON string to QuartoDocumentInfo
    let parseDocumentJson (jsonStr: string) : Result<QuartoDocumentProvider.Root, string> =
        try
            Ok(QuartoDocumentProvider.Parse(jsonStr))
        with ex ->
            Error $"Failed to parse document JSON: {ex.Message}"
