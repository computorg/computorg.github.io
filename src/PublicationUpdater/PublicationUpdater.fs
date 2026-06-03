namespace PublicationUpdater

open Octokit
open System
open System.Globalization
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open System.Threading.Tasks
open DotNetEnv
open FSharp.Data
open DrBiber
open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions

module Generator =
    open QuartoInspect.QuartoClient

    type Publication =
        { title: string
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
          draft: string }

    let private computoUrl = "https://computo-journal.org/"
    let private publishedRe = Regex(@"^published(_|-)\d+")
    let private redirectStringRe = Regex(@"URL='(.*)'")

    let (|Empty|True|False|Other|) (raw: string) =
        match raw with
        | _ when String.IsNullOrWhiteSpace raw -> Empty
        | _ when raw.Equals("true", StringComparison.OrdinalIgnoreCase) -> True
        | _ when raw.Equals("false", StringComparison.OrdinalIgnoreCase) -> False
        | _ -> Other raw

    let normalizeDraftValue raw =
        match raw with
        | Empty -> "false"
        | True -> "true"
        | False -> "false"
        | Other s -> s.ToLowerInvariant()

    let buildRssTitle (title: string) (authors: string) =
        if String.IsNullOrWhiteSpace(authors) then
            title
        else
            title + " - " + authors

    let private getJsonString (element: JsonElement) (key: string) : string =
        try
            let mutable prop = Unchecked.defaultof<JsonElement>

            if element.TryGetProperty(key, &prop) then
                match prop.ValueKind with
                | JsonValueKind.String -> prop.GetString()
                | JsonValueKind.Null -> ""
                | _ -> prop.ToString()
            else
                ""
        with _ ->
            ""

    let private getJsonObject (element: JsonElement) (key: string) : JsonElement option =
        try
            let mutable prop = Unchecked.defaultof<JsonElement>

            if element.TryGetProperty(key, &prop) then
                Some prop
            else
                None
        with _ ->
            None

    let internal getAuthorsFromJson (authorsElement: JsonElement option) : string =
        match authorsElement with
        | None -> ""
        | Some elem when elem.ValueKind = JsonValueKind.Array ->
            let authors =
                elem.EnumerateArray()
                |> Seq.choose (fun authorElem ->
                    match getJsonObject authorElem "name" with
                    | Some nameElem when nameElem.ValueKind = JsonValueKind.String -> Some(nameElem.GetString())
                    | _ when authorElem.ValueKind = JsonValueKind.String -> Some(authorElem.GetString())
                    | _ -> None)
                |> Seq.toList

            match authors with
            | [] -> ""
            | [ single ] -> single
            | list ->
                let lastAuthor = List.last list
                let otherAuthors = List.take (List.length list - 1) list
                (String.concat ", " otherAuthors) + " and " + lastAuthor
        | Some elem when elem.ValueKind = JsonValueKind.String -> elem.GetString()
        | _ -> ""

    let private getBibTeX (page: string) =
        let htmlFirst = HtmlDocument.Load(page)

        let html =
            htmlFirst.CssSelect("meta[http-equiv='refresh']")
            |> Seq.tryHead
            |> Option.map (fun m ->
                printfn "Found meta refresh: %A at %s" m page

                m.Attributes()
                |> Seq.find (fun a -> a.Name() = "content")
                |> fun a -> a.Value()
                |> redirectStringRe.Match
                |> fun mm -> mm.Groups[1].Value
                |> fun p ->
                    printfn "new url to fetch %s" (page + p)
                    HtmlDocument.Load(page + p))
            |> Option.defaultValue htmlFirst

        let bibtexCls = ".bibtex"

        try
            html.CssSelect(bibtexCls).Head.InnerText()
            |> DirtyParser.bibTeXFromString
            |> _.Head
            |> Result.Ok
        with e ->
            Result.Error e.Message

    let private getAbstract (entry: BibTeXEntry) : string =
        try
            entry.Properties["abstract"] |> string
        with _ ->
            ""

    let private getBibTeXFromRepo (repo: Repository) : string =
        match repo.Homepage with
        | null
        | "" -> ""
        | homepage ->
            getBibTeX homepage
            |> function
                | Ok a -> DrBiber.DirtyParser.bibTeXToString [ a ]
                | Error _ -> ""

    let private getAbstractFromRepo (repo: Repository) : string =
        match repo.Homepage with
        | null
        | "" -> ""
        | homepage ->
            getBibTeX homepage
            |> Result.map getAbstract
            |> function
                | Ok a -> a
                | Error _ -> ""

    let internal extractCitationForRepoName (quartoJson: JsonElement) (repoName: string) : Result<JsonElement, string> =
        try
            let hasMetadataFields (elem: JsonElement) =
                (elem.TryGetProperty("title") |> fst)
                || (elem.TryGetProperty("author") |> fst)
                || (elem.TryGetProperty("authors") |> fst)
                || (elem.TryGetProperty("citation") |> fst)
                || (elem.TryGetProperty("formats") |> fst)

            let tryGetMetadataFromElement (elem: JsonElement) =
                if hasMetadataFields elem then
                    Some elem
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
                match getJsonObject quartoJson "config" |> Option.bind tryGetMetadataFromElement with
                | Some cfg -> Ok cfg
                | None ->
                    match getJsonObject quartoJson "fileInformation" with
                    | Some fileInfo ->
                        let candidates =
                            fileInfo.EnumerateObject()
                            |> Seq.toList
                            |> List.choose (fun prop ->
                                tryGetMetadataFromElement prop.Value
                                |> Option.map (fun elem -> (prop.Name, elem)))

                        match preferIndex candidates with
                        | Some(_, elem) -> Ok elem
                        | None -> Error $"No metadata found in fileInformation for {repoName}"
                    | None ->
                        let keys =
                            quartoJson.EnumerateObject() |> Seq.map (fun p -> p.Name) |> String.concat ", "

                        Error $"No metadata found. Available keys: {keys}"
        with e ->
            Error $"Error extracting citation from quarto inspect for {repoName}: {e.Message}"

    let private extractCitation (quartoJson: JsonElement) (repo: Repository) : Result<JsonElement, string> =
        extractCitationForRepoName quartoJson repo.Name

    let private runQuartoInspect (repoPath: string) : Task<Result<JsonElement, string>> =
        task {
            let! inspectResult = runInspect repoPath |> Async.StartAsTask

            match inspectResult with
            | Error e when e.Contains("not a Quarto project") -> return Error "Not a Quarto project (this is expected)"
            | Error e -> return Error e
            | Ok r ->
                try
                    let doc = JsonDocument.Parse(r.jsonContent)
                    return Ok doc.RootElement
                with ex ->
                    return Error $"JSON parse failed: {ex.Message}"
        }

    let private getQuartoFilePathsViaGitTree
        (client: GitHubClient)
        (owner: string)
        (repo: string)
        (defaultBranch: string)
        : Task<string list> =
        task {
            try
                let! reference =
                    client.Git.Reference.Get(owner, repo, $"heads/{defaultBranch}")
                    |> Async.AwaitTask

                let sha = reference.Object.Sha
                let! tree = client.Git.Tree.GetRecursive(owner, repo, sha) |> Async.AwaitTask

                return
                    tree.Tree
                    |> Seq.filter (fun item -> item.Type.Value = TreeType.Blob)
                    |> Seq.map (fun item -> item.Path)
                    |> Seq.filter (fun path -> path.EndsWith(".qmd") || path.EndsWith(".yml") || path.EndsWith(".yaml"))
                    |> Seq.toList
            with _ ->
                return []
        }

    let private getCitationStructure (quartoJson: JsonElement) (repo: Repository) : Result<Publication, string> =
        try
            match extractCitation quartoJson repo with
            | Ok metadata ->
                let dateStr = getJsonString metadata "date"

                let date =
                    if String.IsNullOrWhiteSpace(dateStr) then
                        DateTime.Now.ToString("yyyy-MM-dd")
                    else
                        dateStr

                let year =
                    try
                        DateTime.Parse(date).Year
                    with _ ->
                        DateTime.Now.Year

                Ok
                    { title = getJsonString metadata "title"
                      name = repo.Name
                      authors = getAuthorsFromJson (getJsonObject metadata "authors")
                      journal = "Computo"
                      doi = getJsonString metadata "doi"
                      year = year
                      date = date
                      description = getJsonString metadata "description"
                      ``abstract`` = getAbstractFromRepo repo
                      repo = repo.Name
                      bibtex = getBibTeXFromRepo repo
                      pdf = getJsonString metadata "pdf"
                      url =
                        match repo.Homepage with
                        | null
                        | "" -> repo.HtmlUrl
                        | h -> h
                      draft = normalizeDraftValue (getJsonString metadata "draft") }
            | Error e -> Error e
        with e ->
            Error $"Error processing citation structure for {repo.Name}: {e.Message}"

    let internal serializeToYaml (items: seq<Publication>) =
        let serializer =
            SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build()

        serializer.Serialize(items)

    let private xmlEscape (s: string) =
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;")

    let internal formatRssDate (s: string) =
        try
            DateTime.Parse(s).ToString("r")
        with _ ->
            DateTime.Now.ToString("r")

    let run (rootDir: string) : int =
        try
            CultureInfo.DefaultThreadCurrentCulture <- Globalization.CultureInfo("en-US")

            let envPath = Path.Combine(rootDir, ".env-secret")

            if File.Exists(envPath) then
                Env.Load(envPath) |> ignore

            let client =
                let c = GitHubClient(ProductHeaderValue("computo"))

                match Environment.GetEnvironmentVariable("API_GITHUB_TOKEN") with
                | null
                | "" -> c
                | token ->
                    c.Credentials <- Credentials(token = token)
                    c

            printfn "================================================"
            printfn "Starting Computo Publication Collection"
            printfn "================================================"

            let repos =
                client.Repository.GetAllForOrg("computorg")
                |> Async.AwaitTask
                |> Async.RunSynchronously

            let getReposContents (filter: Repository -> bool) (inputRepos: seq<Repository>) =
                inputRepos
                |> Seq.filter filter
                |> Seq.map (fun repo ->
                    task {
                        let uniqueId = Guid.NewGuid().ToString().Substring(0, 8)
                        let tempDir = Path.Combine(Path.GetTempPath(), $"{repo.Name}-{uniqueId}")
                        Directory.CreateDirectory(tempDir) |> ignore

                        try
                            let! paths =
                                getQuartoFilePathsViaGitTree client repo.Owner.Login repo.Name repo.DefaultBranch

                            if paths.IsEmpty then
                                return Error $"No Quarto files found in {repo.Name}"
                            else
                                for p in paths do
                                    try
                                        let! rawBytes =
                                            client.Repository.Content.GetRawContent(repo.Owner.Login, repo.Name, p)
                                            |> Async.AwaitTask

                                        let localPath = Path.Combine(tempDir, p)
                                        let localDir = Path.GetDirectoryName(localPath)

                                        if not (String.IsNullOrWhiteSpace(localDir)) then
                                            Directory.CreateDirectory(localDir) |> ignore

                                        File.WriteAllBytes(localPath, rawBytes)
                                    with _ ->
                                        ()

                                let! result = runQuartoInspect tempDir
                                return result |> Result.map (fun json -> (json, repo))
                        finally
                            try
                                Directory.Delete(tempDir, true)
                            with _ ->
                                ()
                    }
                    |> Async.AwaitTask)
                |> Async.Parallel
                |> Async.RunSynchronously
                |> Array.toList

            let published =
                repos
                |> getReposContents (fun r -> r.Name |> publishedRe.IsMatch)
                |> List.choose (function
                    | Ok(json, repo) ->
                        match getCitationStructure json repo with
                        | Ok p -> Some p
                        | Error e ->
                            if not (e.Contains("Not a Quarto project")) then
                                printfn "  Error: %s" e

                            None
                    | Error e ->
                        if not (e.Contains("Not a Quarto project")) then
                            printfn "  Error: %s" e

                        None)
                |> List.sortBy _.date
                |> List.rev

            let drafts, publishedOnly = published |> List.partition (fun d -> d.draft = "true")
            printfn "  Found %d published and %d draft papers" publishedOnly.Length drafts.Length

            let writeYaml (relativePath: string) (content: string) =
                let outPath = Path.Combine(rootDir, relativePath)
                File.WriteAllText(outPath, content)
                printfn "  Wrote %s" outPath

            writeYaml "site/published.yml" (serializeToYaml publishedOnly)

            if drafts.IsEmpty then
                writeYaml "site/pipeline.yml" "[]\n"
            else
                writeYaml "site/pipeline.yml" (serializeToYaml drafts)

            let rssItems = publishedOnly |> List.truncate 10

            let rss =
                let sb = StringBuilder()
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>") |> ignore
                sb.AppendLine("<rss version=\"2.0\">") |> ignore
                sb.AppendLine("<channel>") |> ignore
                sb.AppendLine("  <title>Computo Journal - Recent Articles</title>") |> ignore
                sb.AppendLine($"  <link>{computoUrl}</link>") |> ignore

                sb.AppendLine("  <description>Latest published articles from Computo Journal</description>")
                |> ignore

                for item in rssItems do
                    let desc =
                        if item.``abstract`` <> "" then
                            item.``abstract``
                        else
                            item.description

                    sb.AppendLine("  <item>") |> ignore

                    sb.AppendLine($"    <title>{xmlEscape (buildRssTitle item.title item.authors)}</title>")
                    |> ignore

                    sb.AppendLine($"    <link>{xmlEscape item.url}</link>") |> ignore
                    sb.AppendLine($"    <guid>{xmlEscape item.url}</guid>") |> ignore
                    sb.AppendLine($"    <pubDate>{formatRssDate item.date}</pubDate>") |> ignore

                    if desc <> "" then
                        sb.AppendLine($"    <description>{xmlEscape desc}</description>") |> ignore

                    sb.AppendLine("  </item>") |> ignore

                sb.AppendLine("</channel>") |> ignore
                sb.AppendLine("</rss>") |> ignore
                sb.ToString()

            writeYaml "site/published.xml" rss

            let mock =
                repos
                |> getReposContents (fun r -> r.Name.StartsWith("published-paper"))
                |> List.choose (function
                    | Ok(json, repo) -> getCitationStructure json repo |> Result.toOption
                    | Error _ -> None)

            writeYaml "site/mock-papers.yml" (serializeToYaml mock)

            printfn "================================================"
            printfn "Done"
            printfn "================================================"
            0
        with e ->
            eprintfn "Publication update failed: %s" e.Message
            1
