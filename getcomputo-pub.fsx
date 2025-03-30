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
            |> fun p -> HtmlDocument.Load(page + p))
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
       url = d |> getAnotherThing "citation" |> getSomeString "url" |}

let getPublishedRepoContent (repo: Repository) =
    task {
        let repoName = repo.Name
        let owner = repo.Owner.Login
        // get the list of files in the repo
        let! repoContents = client.Repository.Content.GetAllContents(owner, repoName, "/")

        let fileQmd =
            repoContents
            |> Seq.filter (fun f ->
                f.Type.Value.ToString() = ContentType.File.ToString()
                && f.Path.EndsWith(".qmd")
                && not (f.Path.Contains("-supp")))
            |> Seq.tryHead
            |> Option.map (fun f -> f.Path)
            |> function
                | Some path -> Ok path
                | None -> Error "No .qmd file found"

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
                    | f when Array.length f > 1 -> Ok f[1]
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

let publishedFrontMatters =
    repos
    |> List.ofSeq
    |> List.filter (fun (r: Repository) -> r.Name |> publishedRe.IsMatch)
    |> List.map (getPublishedRepoContent >> Async.AwaitTask)
    |> Async.Parallel
    |> Async.RunSynchronously
    |> Array.toList

let serializer = SerializerBuilder().Build()

publishedFrontMatters
|> List.map (function
    | Ok d -> Result.Ok d
    | Error e -> Error $"Error getting front matter: {e}")
|> List.map (
    Result.bind (fun d ->
        try
            d |> extractCitation |> Result.Ok
        with e ->
            let repoName = d["repo"] :?> string
            Result.Error $"Error getting citation structure for {repoName} : {e.Message}")
)
|> List.choose (function
    | Ok d -> Some d
    | Error e ->
        printfn "Error: %s" e
        None)
|> List.sortBy _.date
|> List.rev
|> serializer.Serialize
|> (fun n -> File.WriteAllText(Path.Combine(__SOURCE_DIRECTORY__, "site", "published.yml"), n))
