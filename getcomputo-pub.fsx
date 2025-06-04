#r "nuget: YamlDotNet"
#r "nuget: Octokit"
#r "nuget: DotNetEnv"
#r "nuget: FSharp.Data"
#r "nuget: DrBiber"

open Octokit
open YamlDotNet.Serialization
open System.Collections.Generic
open System.Text.RegularExpressions
open System.IO
open DotNetEnv
open FSharp.Data
open DrBiber
open System.Threading.Tasks

// exit if QUARTO_PROJECT_RENDER_ALL is set in the environment
if System.Environment.GetEnvironmentVariable("QUARTO_PROJECT_RENDER_ALL") = null then
    printfn "QUARTO_PROJECT_RENDER_ALL is not set, exiting."
    exit 0
// Load environment variables from .env file
Env.Load(".env-secret")

let client =
    let client = new GitHubClient(new ProductHeaderValue("computo"))
    // Using environment variable for token is a good security practice
    match System.Environment.GetEnvironmentVariable("GITHUB_TOKEN") with
    | null
    | "" -> client // No authentication
    | token ->
        client.Credentials <- Credentials(token = token)
        client

let computoGithubReposUrl = "https://api.github.com/users/computorg/repos"

let publishedRe = Regex(@"^published(_|-)\d+")

let repos =
    client.Repository.GetAllForOrg("computorg")
    |> Async.AwaitTask
    |> Async.RunSynchronously

let deserializer = DeserializerBuilder().Build()

let getSomething (thing: string) (d: Dictionary<obj, obj>) =
    d
    |> Seq.tryFind (fun kv -> kv.Key = thing)
    |> Option.map (fun kv -> kv.Value)
    |> Option.defaultValue (box "")

let getSomeString t d = getSomething t d :?> string

let getAnotherThing t d =
    getSomething t d :?> Dictionary<obj, obj>

let getAuthor (d: Dictionary<obj, obj>) = d["name"] :?> string

let getAuthors (d: Dictionary<obj, obj>) =
    d |> getSomething "author" :?> List<obj>
    |> Seq.map (fun a -> a :?> Dictionary<obj, obj> |> getAuthor)
    |> Seq.rev
    |> Seq.toList
    |> function
        | [ last ] -> last
        | last :: list -> (String.concat ", " (list |> Seq.ofList |> Seq.rev)) + " and " + last
        | [] -> ""


type RepoBaseError = Repo of string

type RepoError =
    | NoQmdFound of RepoBaseError
    | NoContentFound of RepoBaseError
    | NoFrontMatterFound of RepoBaseError
    | BogusFrontMatter of RepoBaseError

let redirectStringRe = Regex(@"URL='(.*)'")

let getAbstract (page: string) =

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
        |> _.Head.Properties["abstract"]
        |> Result.Ok
    with e ->
        printfn "Error getting abstract from %s: %s" page e.Message
        Result.Error e.Message

let getAbstractFromDict (d: Dictionary<obj, obj>) =
    d["repoObj"] :?> Repository
    |> _.Homepage
    |> getAbstract
    |> function
        | Ok a -> a
        | Error e ->
            printfn "Error getting abstract from %s: %s" (d["repoObj"] :?> Repository).Name e
            ""

let getDateofQmdFromLastCommit (d: Dictionary<obj, obj>) =
    task {
        try
            let repo = d["repoObj"] :?> Repository
            let qmd = d["qmd"] :?> string

            // Create a request specifically for the file
            let commitRequest = CommitRequest()
            commitRequest.Path <- qmd

            // Get commits for the specific file (limited to just 1)
            let! commits = client.Repository.Commit.GetAll(repo.Owner.Login, repo.Name, commitRequest)

            return
                if commits.Count > 0 then
                    commits[0].Commit.Author.Date.DateTime
                else
                    System.DateTime.MinValue
        with ex ->
            printfn "Error getting last commit date: %s" ex.Message
            return System.DateTime.MinValue
    }

let getDate (d: Dictionary<obj, obj>) =
    let dateStr = d |> getSomeString "date"

    if dateStr = "last-modified" then
        d |> getDateofQmdFromLastCommit |> Async.AwaitTask |> Async.RunSynchronously
    else
        dateStr |> System.DateTime.Parse

let extractCitation (d: Dictionary<obj, obj>) =
    let dateTime = d |> getDate

    {| title = d |> getSomeString "title"
       authors = d |> getAuthors
       journal = d |> getAnotherThing "citation" |> getSomeString "container-title"
       year = dateTime.Year
       date = dateTime.ToString("yyyy-MM-dd")
       description = d |> getSomeString "description"
       abstract' = d |> getAbstractFromDict
       repo = d |> getSomeString "repo"
       pdf = d |> getAnotherThing "citation" |> getSomeString "pdf-url"
       url = d |> getAnotherThing "citation" |> getSomeString "url"
       draft = d |> getSomeString "draft" |}

let getPublishedRepoContent (repo: Repository) =
    task {
        let repoName = repo.Name
        let owner = repo.Owner.Login
        // get the list of files in the repo
        let! (repoContents: IReadOnlyList<RepositoryContent>) =
            client.Repository.Content.GetAllContents(owner, repoName, "/")

        let fileQmd =
            repoContents
            |> Seq.filter (fun f ->
                f.Type.Value.ToString() = ContentType.File.ToString()
                && f.Path.EndsWith(".qmd")
                && not (f.Path.Contains("-supp")))
            |> Seq.tryHead
            |> Option.map _.Path
            |> function
                | Some path -> Ok path
                | None -> Error "No .qmd file found"

        let fileQuartoYML =
            repoContents
            |> Seq.filter (fun f -> f.Type.Value.ToString() = ContentType.File.ToString() && f.Path = "_quarto.yml")
            |> Seq.tryHead
            |> Option.map _.Path

        let! quartoYMLMatch =
            match fileQuartoYML with
            | Some path -> client.Repository.Content.GetAllContents(owner, repoName, path)
            | _ -> Task.FromResult([])

        let mainQuartoYML =
            quartoYMLMatch |> Seq.tryHead |> Option.map _.Content |> Option.defaultValue ""

        match fileQmd with
        | Ok path ->
            let! content = client.Repository.Content.GetAllContents(owner, repoName, path)

            return
                content
                |> Seq.tryHead
                |> function
                    | Some c when c.Type.Value.ToString() = ContentType.File.ToString() -> Result.Ok c
                    | _ -> Result.Error "No content found"
                |> Result.map (_.Content >> _.Split("---\n"))
                |> Result.bind (function
                    | f when Array.length f > 1 -> Ok(mainQuartoYML + "\n" + f[1])
                    | _ when mainQuartoYML.Length > 0 -> Ok mainQuartoYML
                    | _ -> Error $"No front matter found for repo {repoName}")
                |> Result.bind (fun f ->
                    try
                        let d = f |> deserializer.Deserialize<Dictionary<obj, obj>>
                        d.Add("repoObj", repo)
                        d.Add("qmd", path)
                        Result.Ok d
                    with e ->
                        Result.Error $"Bogus front matter in {repoName}: {e.Message}")
        | Error e -> return Error e
    }

let getReposContents filter repos =
    repos
    |> List.ofSeq
    |> List.filter filter
    |> List.map (getPublishedRepoContent >> Async.AwaitTask)
    |> Async.Parallel
    |> Async.RunSynchronously
    |> Array.toList


let publishedFrontMatters: Result<Dictionary<obj, obj>, string> list =
    repos |> getReposContents (fun r -> r.Name |> publishedRe.IsMatch)

let getCitationStructure (d: Result<Dictionary<obj, obj>, string>) =
    d
    |> Result.mapError (fun e -> $"Error getting citation structure: {e}")
    |> Result.bind (fun d ->
        try
            d |> extractCitation |> Ok
        with e ->
            let repoName = d["repo"] :?> string
            Error $"Error getting citation structure for {repoName} : {e.Message}")

let serializer = SerializerBuilder().Build()

let publishedYML =
    publishedFrontMatters
    |> List.map getCitationStructure
    |> List.choose (function
        | Ok d -> Some d
        | Error e ->
            printfn "Error: %s" e
            None)
    |> List.sortBy _.date
    |> List.rev
    |> List.partition (fun d -> d.draft = "true")

publishedYML
|> snd
|> serializer.Serialize
|> (fun n -> File.WriteAllText(Path.Combine(__SOURCE_DIRECTORY__, "site", "published.yml"), n))

publishedYML
|> fst
|> serializer.Serialize
|> (fun n -> File.WriteAllText(Path.Combine(__SOURCE_DIRECTORY__, "site", "pipeline.yml"), n))

repos
|> getReposContents (fun r -> r.Name.StartsWith("published-paper"))
|> List.map getCitationStructure
|> List.choose (function
    | Ok d -> Some d
    | Error e ->
        printfn "Error: %s" e
        None)
|> serializer.Serialize
|> (fun n -> File.WriteAllText(Path.Combine(__SOURCE_DIRECTORY__, "site", "mock-papers.yml"), n))

// let mockpapers =
//     repos
//     |> getReposContents (fun r -> r.Name.StartsWith("published-paper"))

// mockpapers
// |> Seq.last
// |> function | Ok d -> getAbstractFromDict d |> printfn "%A" | Error e -> printfn "Error: %s" e
//     // |> List.map getCitationStructure
//     // |> List.choose (function
//     //     | Ok d -> Some d
//     //     | Error e ->
//     //         printfn "Error: %s" e
//     //         None)
//     // |> serializer.Serialize
//     // |> (fun n -> File.WriteAllText(Path.Combine(__SOURCE_DIRECTORY__, "site", "mock-papers.yml"), n))
