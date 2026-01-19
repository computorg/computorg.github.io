#r "nuget: Octokit"
#r "nuget: DotNetEnv"
#r "nuget: FSharp.Data"
#r "nuget: DrBiber"
#r "nuget: YamlDotNet"

open Octokit
open System.Collections.Generic
open System.Text.RegularExpressions
open System.IO
open DotNetEnv
open FSharp.Data
open DrBiber
open System.Threading.Tasks
open System.Text.Json
open System.Text.Json.Serialization
open System.Text
open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions

System.Globalization.CultureInfo.DefaultThreadCurrentCulture <-
    System.Globalization.CultureInfo("en-US")
// exit if QUARTO_PROJECT_RENDER_ALL is set in the environment
// if System.Environment.GetEnvironmentVariable("QUARTO_PROJECT_RENDER_ALL") = null then
//     printfn "QUARTO_PROJECT_RENDER_ALL is not set, exiting."
//     exit 0
// Load environment variables from .env file
Env.Load(".env-secret")

let client =
    let client = new GitHubClient(new ProductHeaderValue("computo"))
    // Using environment variable for token is a good security practice
    match System.Environment.GetEnvironmentVariable("API_GITHUB_TOKEN") with
    | null
    | "" -> client // No authentication
    | token ->
        client.Credentials <- Credentials(token = token)
        client

let computoGithubReposUrl = "https://api.github.com/users/computorg/repos"
let computoUrl = "https://computo-journal.org/"
let publishedRe = Regex(@"^published(_|-)\d+")

printfn "================================================"
printfn "Starting Computo Publication Collection Script"
printfn "================================================"
printfn ""

printfn "[1/5] Fetching repositories from computorg organization..."
let repos =
    client.Repository.GetAllForOrg("computorg")
    |> Async.AwaitTask
    |> Async.RunSynchronously

printfn "✓ Found %d repositories" repos.Count
printfn ""

// Helper to get values from JSON elements with safe type casting
let getJsonString (element: JsonElement) (key: string) : string =
    try
        let mutable prop = Unchecked.defaultof<JsonElement>
        if element.TryGetProperty(key, &prop) then
            match prop.ValueKind with
            | JsonValueKind.String -> prop.GetString()
            | JsonValueKind.Null -> ""
            | _ -> prop.ToString()
        else
            ""
    with
    | _ -> ""

let getJsonObject (element: JsonElement) (key: string) : JsonElement option =
    try
        let mutable prop = Unchecked.defaultof<JsonElement>
        if element.TryGetProperty(key, &prop) then
            Some prop
        else
            None
    with
    | _ -> None

let getJsonArray (element: JsonElement) (key: string) : JsonElement seq =
    try
        let mutable prop = Unchecked.defaultof<JsonElement>
        if element.TryGetProperty(key, &prop) && prop.ValueKind = JsonValueKind.Array then
            prop.EnumerateArray()
        else
            Seq.empty
    with
    | _ -> Seq.empty

// Very lightweight front matter parser for fallback (mock papers)
let tryParseFrontMatter (path: string) : Map<string, string> option =
    try
        let content = System.IO.File.ReadAllText(path)
        let pattern = "(?s)^---\s*(.*?)\s*---"
        let m = System.Text.RegularExpressions.Regex.Match(content, pattern)
        if not m.Success then None
        else
            let fm = m.Groups[1].Value.Split('\n') |> Array.toList
            let mutable currentKey = ""
            let dict = System.Collections.Generic.Dictionary<string, string>()
            for line in fm do
                let trimmed = line.Trim()
                if trimmed.StartsWith("#") || trimmed = "" then () else
                if trimmed.StartsWith("-") && currentKey <> "" then
                    let v = trimmed.TrimStart('-').Trim()
                    let existing = if dict.ContainsKey(currentKey) then dict[currentKey] else ""
                    let combined = if existing = "" then v else existing + "; " + v
                    dict[currentKey] <- combined
                else
                    let parts = trimmed.Split([|':'|], 2)
                    if parts.Length = 2 then
                        currentKey <- parts[0].Trim()
                        dict[currentKey] <- parts[1].Trim()
            dict |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq |> Some
    with _ -> None


type RepoBaseError = Repo of string

type RepoError =
    | NoQmdFound of RepoBaseError
    | NoContentFound of RepoBaseError
    | NoFrontMatterFound of RepoBaseError
    | BogusFrontMatter of RepoBaseError

let redirectStringRe = Regex(@"URL='(.*)'")

let getBibTeX (page: string) =
    let htmlFirst = HtmlDocument.Load(page)
    let html =
        // handle the case of http redirect
        htmlFirst.CssSelect("meta[http-equiv='refresh']")
        |> Seq.tryHead
        |> Option.map (fun m ->
            printfn "Found meta refresh: %A at %s" m page

            m.Attributes()
            |> Seq.find (fun a -> a.Name() = "content")
            |> fun a -> a.Value()
            |> redirectStringRe.Match
            |> fun m -> m.Groups[1].Value
            |> fun p ->
                printfn "new url to fetch %s" (page + p)
                HtmlDocument.Load(page + p))
        |> Option.defaultValue htmlFirst

    try
        html.CssSelect(".bibtex").Head.InnerText()
        |> DirtyParser.bibTeXFromString
        |> _.Head
        |> Result.Ok
    with e ->
        printfn "Error getting BibTeX from %s: %s" page e.Message
        Result.Error e.Message

let getAbstract (entry: BibTeXEntry) : string =
    try
        entry.Properties["abstract"] |> string
    with _ -> ""

// Helper to parse author list from JSON element
let getAuthorsFromJson (authorsElement: JsonElement option) : string =
    match authorsElement with
    | None -> ""
    | Some elem when elem.ValueKind = JsonValueKind.Array ->
        let authors =
            elem.EnumerateArray()
            |> Seq.choose (fun authorElem ->
                match getJsonObject authorElem "name" with
                | Some nameElem when nameElem.ValueKind = JsonValueKind.String -> Some (nameElem.GetString())
                | _ -> if authorElem.ValueKind = JsonValueKind.String then Some (authorElem.GetString()) else None)
            |> Seq.toList

        match authors with
        | [] -> ""
        | [ last ] -> last
        | list ->
            let lastAuthor = List.last list
            let otherAuthors = List.take (List.length list - 1) list
            (String.concat ", " otherAuthors) + " and " + lastAuthor
    | Some elem when elem.ValueKind = JsonValueKind.String -> elem.GetString()
    | _ -> ""

let extractCitation (quartoJson: JsonElement) (repo: Repository) : Result<JsonElement, string> =
    try
        let hasMetadataFields (elem: JsonElement) =
            (elem.TryGetProperty("title") |> fst) ||
            (elem.TryGetProperty("author") |> fst) ||
            (elem.TryGetProperty("authors") |> fst) ||
            (elem.TryGetProperty("citation") |> fst) ||
            (elem.TryGetProperty("formats") |> fst)

        let tryGetMetadataFromElement (elem: JsonElement) =
            if hasMetadataFields elem then Some elem
            else
                match getJsonObject elem "metadata" with
                | Some md when hasMetadataFields md -> Some md
                | _ -> None

        let preferIndex (candidates: (string * JsonElement) list) =
            candidates
            |> List.tryFind (fun (name, _) -> name.EndsWith("index.qmd"))
            |> Option.orElse (candidates |> List.tryHead)

        match tryGetMetadataFromElement quartoJson with
        | Some rootMetadata -> Ok rootMetadata
        | None ->
            // If config itself holds metadata (common when metadata is only in _quarto.yml)
            match getJsonObject quartoJson "config" |> Option.bind tryGetMetadataFromElement with
            | Some cfg -> Ok cfg
            | None ->
                match getJsonObject quartoJson "fileInformation" with
                | Some fileInfo ->
                    let candidates =
                        fileInfo.EnumerateObject()
                        |> Seq.choose (fun p ->
                            match tryGetMetadataFromElement p.Value with
                            | Some md -> Some (p.Name, md)
                            | None -> None)
                        |> Seq.toList

                    match preferIndex candidates with
                    | Some (_, md) -> Ok md
                    | None ->
                        // Fallback: check 'files' array entries that may carry metadata
                        let fileMetadata =
                            getJsonArray quartoJson "files"
                            |> Seq.choose (fun f ->
                                match getJsonObject f "metadata" with
                                | Some md when tryGetMetadataFromElement md |> Option.isSome -> Some (getJsonString f "path", md)
                                | Some md -> Some (getJsonString f "path", md)
                                | _ -> None)
                            |> Seq.toList

                        match preferIndex fileMetadata with
                        | Some (_, md) -> Ok md
                        | None ->
                            // Check config.metadata (e.g., project-level metadata in _quarto.yml)
                            match getJsonObject quartoJson "config" |> Option.bind (fun c -> getJsonObject c "metadata") with
                            | Some cfgMeta when tryGetMetadataFromElement cfgMeta |> Option.isSome -> Ok cfgMeta
                            | _ ->
                                let sampleInfo =
                                    getJsonObject quartoJson "fileInformation"
                                    |> Option.bind (fun fi -> fi.EnumerateObject() |> Seq.tryHead |> Option.map (fun p -> p.Value))
                                    |> Option.map (fun v -> System.Text.Json.JsonSerializer.Serialize(v))
                                    |> Option.defaultValue "<none>"
                                let keys = quartoJson.EnumerateObject() |> Seq.map (fun p -> p.Name) |> String.concat ", "
                                Error $"No metadata found. Available keys: {keys}. Sample fileInformation entry: {sampleInfo}"
                | None ->
                    let keys = quartoJson.EnumerateObject() |> Seq.map (fun p -> p.Name) |> String.concat ", "
                    Error $"No metadata found. Available keys: {keys}"
    with e ->
        Error $"Error extracting citation from quarto inspect for {repo.Name}: {e.Message}"

let getBibTeXFromRepo (repo: Repository) : string =
    match repo.Homepage with
    | null | "" -> ""
    | homepage ->
        getBibTeX homepage
        |> function
            | Ok a -> DrBiber.DirtyParser.bibTeXToString [ a ]
            | Error e ->
                printfn "Error getting BibTeX from %s: %s" repo.Name e
                ""

let getAbstractFromRepo (repo: Repository) : string =
    match repo.Homepage with
    | null | "" -> ""
    | homepage ->
        getBibTeX homepage
        |> Result.map (fun bibTeX -> getAbstract bibTeX)
        |> function
            | Ok a -> a
            | Error e ->
                printfn "Error getting abstract from %s: %s" repo.Name e
                ""

// Run quarto inspect on a repository
let runQuartoInspect (repoPath: string) : Task<Result<JsonElement, string>> =
    task {
        try
            let startTime = System.DateTime.Now
            let tempDir = System.IO.Path.GetTempPath()
            let outputFile = System.IO.Path.Combine(tempDir, $"quarto-inspect-{System.Guid.NewGuid()}.json")

            // Run quarto inspect and capture output
            let processInfo = System.Diagnostics.ProcessStartInfo()
            processInfo.FileName <- "quarto"
            processInfo.Arguments <- $"inspect \"{repoPath}\" \"{outputFile}\""
            processInfo.RedirectStandardOutput <- true
            processInfo.RedirectStandardError <- true
            processInfo.UseShellExecute <- false
            processInfo.CreateNoWindow <- true

            printfn "      Starting quarto inspect process..."
            use proc = System.Diagnostics.Process.Start(processInfo)
            do! proc.WaitForExitAsync()
            printfn "      Quarto inspect process completed in %A" (System.DateTime.Now - startTime)

            if proc.ExitCode <> 0 then
                let error = proc.StandardError.ReadToEnd()
                let output = proc.StandardOutput.ReadToEnd()
                
                // Check if it's a "not a Quarto project" error - this is expected for non-Quarto repos
                if error.Contains("not a Quarto project") then
                    return Error $"Not a Quarto project (this is expected)"
                else
                    return Error $"quarto inspect failed (exit code {proc.ExitCode}): {error} {output}"
            else
                if System.IO.File.Exists(outputFile) then
                    // Read the output JSON file
                    let json = System.IO.File.ReadAllText(outputFile)
                    System.IO.File.Delete(outputFile)
                    
                    let doc = JsonDocument.Parse(json)
                    return Ok doc.RootElement
                else
                    return Error $"quarto inspect output file not found at {outputFile}"
        with e ->
            return Error $"Error running quarto inspect: {e.Message}"
    }

let getQuartoFilePathsViaGitTree (owner: string) (repo: string) (defaultBranch: string) : Task<string list> =
    task {
        try
            let! reference = client.Git.Reference.Get(owner, repo, $"heads/{defaultBranch}") |> Async.AwaitTask
            let sha = reference.Object.Sha
            let! tree = client.Git.Tree.GetRecursive(owner, repo, sha) |> Async.AwaitTask

            let quartoFiles =
                tree.Tree
                |> Seq.filter (fun item -> item.Type.Value = TreeType.Blob)
                |> Seq.map (fun item -> item.Path)
                |> Seq.filter (fun path ->
                    path.EndsWith(".qmd") ||
                    path.EndsWith(".yml") ||
                    path.EndsWith(".yaml"))
                |> Seq.toList

            return quartoFiles
        with _ ->
            return []
    }

let getPublishedRepoContent (repo: Repository) : Task<Result<JsonElement * Repository * Map<string, string> option, string>> =
    task {
        try
            let startTime = System.DateTime.Now
            printfn "  Processing repo: %s" repo.Name
            
            // Create a temporary directory for this repo with unique identifier
            let uniqueId = System.Guid.NewGuid().ToString().Substring(0, 8)
            let tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{repo.Name}-{uniqueId}")
            
            // Clean up any existing directory first
            try
                if System.IO.Directory.Exists(tempDir) then
                    System.IO.Directory.Delete(tempDir, true)
            with _ -> ()
            
            System.IO.Directory.CreateDirectory(tempDir) |> ignore

            printfn "    [%s] Fetching repository content via API..." (System.DateTime.Now.ToString("HH:mm:ss"))
            
            // Get all Quarto file paths via Git tree (fast, no recursion)
            let! quartoFiles = getQuartoFilePathsViaGitTree repo.Owner.Login repo.Name repo.DefaultBranch

            if quartoFiles.IsEmpty then
                printfn "    [%s] ⚠ No Quarto files found, skipping" (System.DateTime.Now.ToString("HH:mm:ss"))
                try
                    System.IO.Directory.Delete(tempDir, true)
                with _ -> ()
                return Error $"No Quarto files found in {repo.Name}"
            else
                printfn "    [%s] Found %d Quarto files, downloading..." (System.DateTime.Now.ToString("HH:mm:ss")) quartoFiles.Length
                
                // Download each Quarto file
                let mutable downloadedCount = 0
                let mutable firstQmdPath : string option = None
                for path in quartoFiles do
                    try
                        let! rawBytes = client.Repository.Content.GetRawContent(repo.Owner.Login, repo.Name, path) |> Async.AwaitTask
                        let localPath = System.IO.Path.Combine(tempDir, path)
                        let localDir = System.IO.Path.GetDirectoryName(localPath)
                        if not (System.String.IsNullOrEmpty(localDir)) then
                            System.IO.Directory.CreateDirectory(localDir) |> ignore
                        System.IO.File.WriteAllBytes(localPath, rawBytes)
                        if localPath.EndsWith(".qmd") && firstQmdPath.IsNone then firstQmdPath <- Some localPath
                        downloadedCount <- downloadedCount + 1
                    with ex ->
                        printfn "    [%s] Warning: Could not download %s: %s" (System.DateTime.Now.ToString("HH:mm:ss")) path ex.Message

                let downloadElapsed = System.DateTime.Now - startTime
                printfn "    [%s] Downloaded %d files (%A elapsed)" (System.DateTime.Now.ToString("HH:mm:ss")) downloadedCount downloadElapsed
                
                if downloadedCount = 0 then
                    try
                        System.IO.Directory.Delete(tempDir, true)
                    with _ -> ()
                    return Error $"Could not download any Quarto files from {repo.Name}"
                else
                    let frontMatter =
                        match firstQmdPath with
                        | Some p -> tryParseFrontMatter p
                        | None -> None

                    // Run quarto inspect on the repo
                    printfn "    [%s] Running quarto inspect..." (System.DateTime.Now.ToString("HH:mm:ss"))
                    let! quartoResult = runQuartoInspect tempDir

                    // Clean up
                    try
                        System.IO.Directory.Delete(tempDir, true)
                    with _ ->
                        ()

                    match quartoResult with
                    | Ok json -> 
                        let elapsed = System.DateTime.Now - startTime
                        printfn "    [%s] ✓ Successfully inspected (total: %A)" (System.DateTime.Now.ToString("HH:mm:ss")) elapsed
                        return Ok (json, repo, frontMatter)
                    | Error e -> 
                        printfn "    [%s] ✗ Quarto inspect error: %s" (System.DateTime.Now.ToString("HH:mm:ss")) e
                        return Error e
        with e ->
            printfn "    [%s] ✗ Exception: %s" (System.DateTime.Now.ToString("HH:mm:ss")) e.Message
            return Error $"Error processing repo {repo.Name}: {e.Message}"
    }

let getReposContents filter repos =
    repos
    |> List.ofSeq
    |> List.filter filter
    |> fun filtered -> 
        printfn "[2/5] Processing %d repositories..." filtered.Length
        filtered
    |> List.map (getPublishedRepoContent >> Async.AwaitTask)
    |> Async.Parallel
    |> Async.RunSynchronously
    |> Array.toList


let publishedRepos: Result<(JsonElement * Repository * Map<string,string> option), string> list =
    repos |> getReposContents (fun r -> r.Name |> publishedRe.IsMatch)

printfn ""
printfn "[3/5] Extracting citation structures..."

// Publication record used for YAML and RSS outputs
type Publication = {
    title: string
    name: string
    authors: string
    journal: string
    doi: string
    year: int
    date: string
    description: string
    ``abstract``: string
    repo: string
    bibtex: string
    pdf: string
    url: string
    draft: string
}

let getCitationStructure (result: Result<JsonElement * Repository * Map<string,string> option, string>) =
    result
    |> Result.mapError (fun e -> $"Error getting citation structure: {e}")
    |> Result.bind (fun (quartoJson, repo, frontMatter) ->
        try
            match extractCitation quartoJson repo with
            | Ok metadata ->
                let dateStr = getJsonString metadata "date"
                let dateTime =
                    if dateStr = "last-modified" || dateStr = "" then
                        System.DateTime.Now
                    else
                        try
                            System.DateTime.Parse(dateStr)
                        with _ ->
                            System.DateTime.Now

                let title = getJsonString metadata "title"
                let repoName =
                    let fromMeta = getJsonString metadata "repo"
                    if System.String.IsNullOrWhiteSpace(fromMeta) then repo.Name else fromMeta
                let description = getJsonString metadata "description"
                let draft =
                    let raw = getJsonString metadata "draft"
                    if System.String.IsNullOrWhiteSpace(raw) then "false"
                    elif raw.Equals("true", System.StringComparison.OrdinalIgnoreCase) then "true"
                    elif raw.Equals("false", System.StringComparison.OrdinalIgnoreCase) then "false"
                    else raw.ToLowerInvariant()
                let authors =
                    let primary = getAuthorsFromJson (getJsonObject metadata "author")
                    if System.String.IsNullOrWhiteSpace(primary) then
                        getAuthorsFromJson (getJsonObject metadata "authors")
                    else primary
                let citationObj = getJsonObject metadata "citation"
                
                let journal = citationObj |> Option.bind (fun c -> getJsonObject c "container-title") |> Option.map (fun j -> j.GetString()) |> Option.defaultValue ""
                let doi = citationObj |> Option.bind (fun c -> getJsonObject c "doi") |> Option.map (fun d -> d.GetString()) |> Option.defaultValue ""
                let pdfUrl = citationObj |> Option.bind (fun c -> getJsonObject c "pdf-url") |> Option.map (fun p -> p.GetString()) |> Option.defaultValue ""

                let bibtexFromMeta =
                    citationObj
                    |> Option.bind (fun c ->
                        let b1 = getJsonString c "bibtex"
                        let b2 = getJsonString c "citation-entry"
                        if not (System.String.IsNullOrWhiteSpace b1) then Some b1
                        elif not (System.String.IsNullOrWhiteSpace b2) then Some b2
                        else None)
                    |> Option.defaultValue ""

                let bibtex = if bibtexFromMeta <> "" then bibtexFromMeta else getBibTeXFromRepo repo

                if draft = "true" then
                    printfn "  [DRAFT] %s" title
                else
                    printfn "  [PUBLISHED] %s" title

                { title = title
                  name = title
                  authors = authors
                  journal = journal
                  doi = doi
                  year = dateTime.Year
                  date = dateTime.ToString("yyyy-MM-dd")
                  description = description
                  ``abstract`` = getAbstractFromRepo repo
                  repo = repoName
                  bibtex = bibtex
                  pdf = pdfUrl
                  url = computoUrl + repoName
                  draft = draft }
                |> Ok
            | Error _ ->
                // Fallback to front matter if available
                match frontMatter with
                | None -> Error "No metadata found"
                | Some fm ->
                    let tryGet key = fm |> Map.tryFind key |> Option.defaultValue ""
                    let title = tryGet "title"
                    let authors = tryGet "author"
                    let dateStr = tryGet "date"
                    let dateTime =
                        if dateStr = "" then System.DateTime.Now
                        else
                            try System.DateTime.Parse(dateStr) with _ -> System.DateTime.Now

                    let bibtex = tryGet "bibtex"
                    let desc = tryGet "description"

                    { title = title
                      name = title
                      authors = authors
                      journal = ""
                      doi = ""
                      year = dateTime.Year
                      date = dateTime.ToString("yyyy-MM-dd")
                      description = desc
                      ``abstract`` = ""
                      repo = repo.Name
                      bibtex = if bibtex = "" then getBibTeXFromRepo repo else bibtex
                      pdf = ""
                      url = computoUrl + repo.Name
                      draft = "true" }
                    |> Ok
        with e ->
            Error $"Error processing citation structure for {repo.Name}: {e.Message}")

let serializer =
    let options = JsonSerializerOptions()
    options.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
    options.WriteIndented <- true
    options

let publishedYML =
    publishedRepos
    |> List.map getCitationStructure
    |> List.choose (function
        | Ok d -> Some d
        | Error e ->
            // Suppress "not a Quarto project" errors as they're expected
            if not (e.Contains("Not a Quarto project")) then
                printfn "  ✗ Error: %s" e
            None)
    |> List.sortBy _.date
    |> List.rev
    |> List.partition (fun d -> d.draft = "true")

printfn ""
printfn "[4/5] Writing output files..."

// Partition results
let drafts = publishedYML |> fst
let publishedOnly = publishedYML |> snd
let draftCount = drafts |> List.length
let publishedCount = publishedOnly |> List.length

printfn "  Found %d published and %d draft papers" publishedCount draftCount

// Serialize to YAML using YamlDotNet
let serializeToYaml (items: seq<Publication>) =
    let serializer =
        SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build()
    serializer.Serialize(items)

publishedOnly
|> serializeToYaml
|> (fun n -> 
    let path = Path.Combine(__SOURCE_DIRECTORY__, "site", "published.yml")
    File.WriteAllText(path, n)
    printfn "  ✓ Wrote %s (%d published papers)" path publishedCount)

let pipelinePath = Path.Combine(__SOURCE_DIRECTORY__, "site", "pipeline.yml")
if draftCount = 0 then
    File.WriteAllText(pipelinePath, "[]\n")
    printfn "  ✓ Wrote %s (%d draft papers)" pipelinePath draftCount
else
    drafts
    |> serializeToYaml
    |> (fun n -> 
        File.WriteAllText(pipelinePath, n)
        printfn "  ✓ Wrote %s (%d draft papers)" pipelinePath draftCount)

// Generate RSS (top 10 most recent published)
let xmlEscape (s: string) =
    s.Replace("&", "&amp;")
     .Replace("<", "&lt;")
     .Replace(">", "&gt;")
     .Replace("\"", "&quot;")
     .Replace("'", "&apos;")

let formatRssDate (s: string) =
    try
        System.DateTime.Parse(s).ToString("r")
    with _ -> System.DateTime.Now.ToString("r")

let rssContent =
    let items = publishedOnly |> List.truncate 10
    let sb = StringBuilder()
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>") |> ignore
    sb.AppendLine("<rss version=\"2.0\">") |> ignore
    sb.AppendLine("<channel>") |> ignore
    sb.AppendLine("  <title>Computo Journal - Recent Articles</title>") |> ignore
    sb.AppendLine($"  <link>{computoUrl}</link>") |> ignore
    sb.AppendLine("  <description>Latest published articles from Computo Journal</description>") |> ignore
    sb.AppendLine("  <generator>getcomputo-pub.fsx</generator>") |> ignore

    for item in items do
        let desc = if item.``abstract`` <> "" then item.``abstract`` else item.description
        let titleWithAuthors =
            if System.String.IsNullOrWhiteSpace(item.authors) then item.title
            else item.title + " — " + item.authors
        sb.AppendLine("  <item>") |> ignore
        sb.AppendLine($"    <title>{xmlEscape titleWithAuthors}</title>") |> ignore
        sb.AppendLine($"    <link>{xmlEscape item.url}</link>") |> ignore
        sb.AppendLine($"    <guid>{xmlEscape item.url}</guid>") |> ignore
        sb.AppendLine($"    <pubDate>{formatRssDate item.date}</pubDate>") |> ignore
        if desc <> "" then sb.AppendLine($"    <description>{xmlEscape desc}</description>") |> ignore
        sb.AppendLine("  </item>") |> ignore

    sb.AppendLine("</channel>") |> ignore
    sb.AppendLine("</rss>") |> ignore
    sb.ToString()

let rssPath = Path.Combine(__SOURCE_DIRECTORY__, "site", "published.xml")
File.WriteAllText(rssPath, rssContent)
printfn "  ✓ Wrote %s (10 most recent)" rssPath

printfn ""
printfn "[5/5] Processing mock papers..."

repos
|> getReposContents (fun r -> r.Name.StartsWith("published-paper"))
|> List.map getCitationStructure
|> List.choose (function
    | Ok d -> Some d
    | Error e ->
        // Suppress "not a Quarto project" errors as they're expected
        if not (e.Contains("Not a Quarto project")) then
            printfn "  ✗ Error: %s" e
        None)
|> fun mockPapers ->
    let count = List.length mockPapers
    printfn "  Found %d mock papers" count
    mockPapers
|> serializeToYaml
|> (fun n -> 
    let path = Path.Combine(__SOURCE_DIRECTORY__, "site", "mock-papers.yml")
    File.WriteAllText(path, n)
    printfn "  ✓ Wrote %s" path)

printfn ""
printfn "================================================"
printfn "✓ Script completed successfully!"
printfn "================================================"
