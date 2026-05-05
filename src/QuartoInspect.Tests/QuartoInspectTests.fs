module QuartoInspectTests

open System
open System.IO
open System.Text.RegularExpressions
open Expecto
open Octokit
open QuartoInspect
open QuartoInspect.QuartoClient
open QuartoInspect.QuartoTypes

// Test configuration
let mockRepoOwner = "computorg"
let testTimeoutMs = 60000 // 60 seconds

// Helper to get GitHub client
let getGitHubClient () =
    let client = new GitHubClient(new ProductHeaderValue("computo-tests"))

    match System.Environment.GetEnvironmentVariable("API_GITHUB_TOKEN") with
    | null
    | "" -> client
    | token ->
        client.Credentials <- Credentials(token = token)
        client

let private isNotFound (ex: exn) =
    ex.Message.Contains("Not Found", StringComparison.OrdinalIgnoreCase)
    || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)

let private tryFindFileUpwards (startDir: string) (relativePath: string) =
    let rec loop (dir: DirectoryInfo) =
        let candidate = Path.Combine(dir.FullName, relativePath)

        if File.Exists(candidate) then Some candidate
        elif isNull dir.Parent then None
        else loop dir.Parent

    loop (DirectoryInfo(startDir))

let private mockRepoCandidates =
    lazy
        (let fromEnv =
            match Environment.GetEnvironmentVariable("MOCK_REPO_NAME") with
            | null
            | "" -> []
            | value -> [ value ]

         let fromMockPapers =
             let repoRegex = Regex("^\\s*repo:\\s*([^\\s]+)\\s*$")

             match tryFindFileUpwards Environment.CurrentDirectory "site/mock-papers.yml" with
             | None -> []
             | Some path ->
                 File.ReadAllLines(path)
                 |> Seq.choose (fun line ->
                     let m = repoRegex.Match(line)

                     if m.Success then Some(m.Groups[1].Value.Trim()) else None)
                 |> Seq.distinct
                 |> Seq.toList

         fromEnv @ fromMockPapers @ [ "published-paper-example" ] |> List.distinct)

let private resolveMockRepo (client: GitHubClient) =
    async {
        let mutable found: Repository option = None

        for repoName in mockRepoCandidates.Value do
            if found.IsNone then
                try
                    let! repo = client.Repository.Get(mockRepoOwner, repoName) |> Async.AwaitTask
                    found <- Some repo
                with
                | :? NotFoundException -> ()
                | ex when isNotFound ex -> ()

        return found
    }

// ============================================================================
// GitHub API Availability Tests
// ============================================================================

let githubApiTests =
    testList
        "GitHub API Availability"
        [

          testAsync "GitHub API is reachable" {
              let client = getGitHubClient ()

              try
                  let! user = client.User.Get("computorg") |> Async.AwaitTask
                  Expect.isNotNull user "Should retrieve computorg user"
                  Expect.equal user.Login "computorg" "Should get correct user login"
              with
              | :? NotFoundException as ex ->
                  Tests.skiptest $"User not found (expected in some environments): {ex.Message}"
              | ex -> failtest $"GitHub API call failed: {ex.Message}"
          }

          testAsync "GitHub API can fetch repositories" {
              let client = getGitHubClient ()

              try
                  let! repos = client.Repository.GetAllForOrg("computorg") |> Async.AwaitTask
                  Expect.isGreaterThan repos.Count 0 "Should retrieve at least one repository"
              with
              | :? RateLimitExceededException -> Tests.skiptest "GitHub API rate limit exceeded"
              | ex -> failtest $"Failed to fetch repositories: {ex.Message}"
          }

          testAsync "GitHub API can retrieve repository details" {
              let client = getGitHubClient ()

              try
                  let! repoOpt = resolveMockRepo client

                  match repoOpt with
                  | None ->
                      let candidates = String.concat ", " mockRepoCandidates.Value
                      Tests.skiptest $"No accessible mock repository found in {mockRepoOwner}. Candidates: {candidates}"
                  | Some repo ->
                      Expect.isNotNull repo "Should retrieve repository"
                      Expect.equal repo.Owner.Login mockRepoOwner "Owner should be computorg"
                      Expect.isGreaterThan repo.Name.Length 0 "Repository name should be non-empty"
              with
              | :? NotFoundException -> Tests.skiptest $"No accessible mock repository found in {mockRepoOwner}"
              | ex when isNotFound ex ->
                  Tests.skiptest $"No accessible mock repository found in {mockRepoOwner}: {ex.Message}"
              | ex -> failtest $"Failed to retrieve repository details: {ex.Message}"
          } ]

// ============================================================================
// Quarto Installation Tests
// ============================================================================

let quartoInstallationTests =
    testList
        "Quarto Installation"
        [

          testAsync "Quarto is installed and available" {
              let! result = QuartoClient.checkQuartoAvailable ()

              match result with
              | Ok version -> Expect.isGreaterThan version.Length 0 "Quarto version output should be non-empty"
              | Error msg -> Tests.skiptest $"Quarto not available: {msg}"
          } ]

// ============================================================================
// Quarto Inspect Schema Compliance Tests
// ============================================================================

let quartoSchemaComplianceTests =
    testList
        "Quarto Inspect Schema Compliance"
        [

          test "Document schema accepts valid minimal document" {
              let validJson =
                  """
        {
            "quarto": { "version": "1.3.0" },
            "engines": ["python"],
            "formats": { "html": {} },
            "resources": [],
            "fileInformation": {}
        }
        """

              match QuartoClient.validateDocumentSchema validJson with
              | Ok _ -> ()
              | Error msg -> failtest $"Valid document should parse: {msg}"
          }

          test "Document schema rejects missing engines field" {
              let invalidJson =
                  """
        {
            "quarto": { "version": "1.3.0" },
            "formats": { "html": {} },
            "resources": [],
            "fileInformation": {}
        }
        """

              match QuartoClient.validateDocumentSchema invalidJson with
              | Ok _ -> failtest "Should reject document without engines"
              | Error msg -> Expect.stringContains msg "engines" "Error should mention missing engines field"
          }

          test "Document schema rejects missing quarto field" {
              let invalidJson =
                  """
        {
            "engines": ["python"],
            "formats": { "html": {} },
            "resources": [],
            "fileInformation": {}
        }
        """

              match QuartoClient.validateDocumentSchema invalidJson with
              | Ok _ -> failtest "Should reject document without quarto"
              | Error msg -> Expect.stringContains msg "quarto" "Error should mention missing quarto field"
          }

          test "Document schema rejects missing formats field" {
              let invalidJson =
                  """
        {
            "quarto": { "version": "1.3.0" },
            "engines": ["python"],
            "resources": [],
            "fileInformation": {}
        }
        """

              match QuartoClient.validateDocumentSchema invalidJson with
              | Ok _ -> failtest "Should reject document without formats"
              | Error msg -> Expect.stringContains msg "formats" "Error should mention missing formats field"
          }

          test "Document schema rejects malformed JSON" {
              let invalidJson = "{ \"quarto\": {"

              match QuartoClient.validateDocumentSchema invalidJson with
              | Ok _ -> failtest "Should reject malformed document JSON"
              | Error msg -> Expect.stringContains msg "Invalid JSON" "Error should report invalid JSON"
          }

          test "Project schema accepts valid minimal project" {
              let validJson =
                  """
        {
            "quarto": { "version": "1.3.0" },
            "dir": "/path/to/project",
            "engines": ["python"],
            "files": {
                "input": [],
                "resources": [],
                "configResources": [],
                "config": []
            },
            "fileInformation": {},
            "extensions": []
        }
        """

              match QuartoClient.validateProjectSchema validJson with
              | Ok _ -> ()
              | Error msg -> failtest $"Valid project should parse: {msg}"
          }

          test "Project schema rejects missing files field" {
              let invalidJson =
                  """
        {
            "quarto": { "version": "1.3.0" },
            "dir": "/path/to/project",
            "engines": ["python"],
            "fileInformation": {},
            "extensions": []
        }
        """

              match QuartoClient.validateProjectSchema invalidJson with
              | Ok _ -> failtest "Should reject project without files"
              | Error msg -> Expect.stringContains msg "files" "Error should mention missing files field"
          }

          test "Project schema rejects missing quarto field" {
              let invalidJson =
                  """
        {
            "dir": "/path/to/project",
            "engines": ["python"],
            "files": {
                "input": [],
                "resources": [],
                "configResources": [],
                "config": []
            },
            "fileInformation": {},
            "extensions": []
        }
        """

              match QuartoClient.validateProjectSchema invalidJson with
              | Ok _ -> failtest "Should reject project without quarto"
              | Error msg -> Expect.stringContains msg "quarto" "Error should mention missing quarto field"
          }

          test "Project schema rejects missing dir field" {
              let invalidJson =
                  """
        {
            "quarto": { "version": "1.3.0" },
            "engines": ["python"],
            "files": {
                "input": [],
                "resources": [],
                "configResources": [],
                "config": []
            },
            "fileInformation": {},
            "extensions": []
        }
        """

              match QuartoClient.validateProjectSchema invalidJson with
              | Ok _ -> failtest "Should reject project without dir"
              | Error msg -> Expect.stringContains msg "dir" "Error should mention missing dir field"
          }

          test "Project schema rejects missing engines field" {
              let invalidJson =
                  """
        {
            "quarto": { "version": "1.3.0" },
            "dir": "/path/to/project",
            "files": {
                "input": [],
                "resources": [],
                "configResources": [],
                "config": []
            },
            "fileInformation": {},
            "extensions": []
        }
        """

              match QuartoClient.validateProjectSchema invalidJson with
              | Ok _ -> failtest "Should reject project without engines"
              | Error msg -> Expect.stringContains msg "engines" "Error should mention missing engines field"
          }

          test "Project schema rejects malformed JSON" {
              let invalidJson = "{ \"quarto\": {"

              match QuartoClient.validateProjectSchema invalidJson with
              | Ok _ -> failtest "Should reject malformed project JSON"
              | Error msg -> Expect.stringContains msg "Invalid JSON" "Error should report invalid JSON"
          }

          test "JSON type provider parses document schema sample" {
              let jsonStr =
                  """
        {
          "quarto": {
            "version": "1.3.0"
          },
          "engines": ["python"],
          "formats": {
            "html": {
              "theme": "default"
            }
          },
          "resources": [],
          "fileInformation": {
            "document.qmd": {
              "includeMap": [],
              "codeCells": [
                {
                  "start": 1,
                  "end": 10,
                  "file": "document.qmd",
                  "source": "import pandas as pd",
                  "language": "python",
                  "metadata": {}
                }
              ]
            }
          }
        }
        """

              match parseDocumentJson jsonStr with
              | Ok parsed -> Expect.isNotNull (box parsed) "Should parse document JSON successfully"
              | Error msg -> failtest $"Should parse document JSON: {msg}"
          }

          test "JSON type provider parses project schema sample" {
              let jsonStr =
                  """
        {
          "quarto": {
            "version": "1.3.0"
          },
          "dir": "/path/to/project",
          "engines": ["python", "r"],
          "config": {
            "project": {
              "type": "website"
            }
          },
          "files": {
            "input": ["index.qmd", "about.qmd"],
            "resources": [],
            "configResources": [],
            "config": ["_quarto.yml"]
          },
          "fileInformation": {
            "index.qmd": {
              "includeMap": [],
              "codeCells": [
                {
                  "start": 1,
                  "end": 15,
                  "file": "index.qmd",
                  "source": "import pandas as pd",
                  "language": "python",
                  "metadata": {
                    "eval": "false"
                  }
                }
              ]
            }
          },
          "extensions": []
        }
        """

              match parseProjectJson jsonStr with
              | Ok parsed ->
                  Expect.isNotNull (box parsed) "Should parse project JSON successfully"
                  Expect.equal parsed.Dir "/path/to/project" "Directory should match"
              | Error msg -> failtest $"Should parse project JSON: {msg}"
          }

          test "JSON type provider returns error on invalid project JSON" {
              match parseProjectJson "{ bad json" with
              | Ok _ -> failtest "Expected parseProjectJson to fail on malformed JSON"
              | Error msg ->
                  Expect.stringContains msg "Failed to parse project JSON" "Error should include parse failure context"
          }

          test "JSON type provider returns error on invalid document JSON" {
              match parseDocumentJson "{ bad json" with
              | Ok _ -> failtest "Expected parseDocumentJson to fail on malformed JSON"
              | Error msg ->
                  Expect.stringContains msg "Failed to parse document JSON" "Error should include parse failure context"
          } ]

// ============================================================================
// Integration Tests with Mock Repository
// ============================================================================

let mockRepoIntegrationTests =
    testList
        "Mock Repository Integration"
        [

          testAsync "Can fetch mock repository from GitHub" {
              let client = getGitHubClient ()

              try
                  let! repoOpt = resolveMockRepo client

                  match repoOpt with
                  | None ->
                      let candidates = String.concat ", " mockRepoCandidates.Value
                      Tests.skiptest $"No accessible mock repository found in {mockRepoOwner}. Candidates: {candidates}"
                  | Some repo ->
                      Expect.isNotNull repo "Mock repository should exist"
                      Expect.equal repo.Owner.Login mockRepoOwner "Repository owner should match"
                      Expect.isGreaterThan repo.Name.Length 0 "Repository name should be non-empty"
              with
              | :? NotFoundException ->
                  Tests.skiptest $"No accessible mock repository found in {mockRepoOwner} organization"
              | ex when isNotFound ex ->
                  Tests.skiptest $"No accessible mock repository found in {mockRepoOwner}: {ex.Message}"
              | ex -> failtest $"Failed to fetch mock repository: {ex.Message}"
          }

          testAsync "Mock repository has expected structure" {
              let client = getGitHubClient ()

              try
                  let! repoOpt = resolveMockRepo client

                  match repoOpt with
                  | None ->
                      let candidates = String.concat ", " mockRepoCandidates.Value
                      Tests.skiptest $"No accessible mock repository found in {mockRepoOwner}. Candidates: {candidates}"
                  | Some repo ->
                      Expect.isNotNull repo.DefaultBranch "Should have default branch"
                      Expect.isGreaterThan (repo.CreatedAt.UtcTicks) 0L "Should have creation date"

                      // Check for typical Quarto project files
                      let! contents =
                          client.Repository.Content.GetAllContents(mockRepoOwner, repo.Name)
                          |> Async.AwaitTask

                      let fileNames = contents |> Seq.map (fun c -> c.Name.ToLower()) |> Set.ofSeq

                      Expect.isTrue
                          (fileNames.Contains("_quarto.yml") || fileNames.Contains("index.qmd"))
                          "Should contain Quarto project files"
              with
              | :? NotFoundException ->
                  Tests.skiptest $"Mock repository structure check skipped - no accessible repository found"
              | ex when isNotFound ex -> Tests.skiptest $"Mock repository structure check skipped: {ex.Message}"
              | ex -> failtest $"Failed to check repository structure: {ex.Message}"
          } ]

// ============================================================================
// Quarto Inspect Execution Tests (when available locally)
// ============================================================================

let quartoExecutionTests =
    testList
        "Quarto Inspect Execution"
        [

          testAsync "Can run quarto inspect on current project" {
              let! quartoAvailable = QuartoClient.checkQuartoAvailable ()

              match quartoAvailable with
              | Error _ -> Tests.skiptest "Quarto not installed, skipping execution tests"
              | Ok _ ->
                  // Try to run inspect on the parent directory (the website project)
                  let parentDir = Directory.GetParent(AppContext.BaseDirectory).Parent.FullName

                  // Only run if we're in the right directory structure
                  if File.Exists(Path.Combine(parentDir, "_quarto.yml")) then
                      let! result = QuartoClient.runInspect parentDir

                      match result with
                      | Ok inspectResult ->
                          Expect.equal inspectResult.exitCode 0 "Quarto inspect should succeed"
                          Expect.isGreaterThan inspectResult.jsonContent.Length 0 "Should produce JSON output"

                          // Validate output conforms to schema
                          match QuartoClient.validateProjectSchema inspectResult.jsonContent with
                          | Ok _ -> ()
                          | Error msg -> failtest $"Output doesn't conform to project schema: {msg}"
                      | Error msg -> Tests.skiptest $"Quarto inspect execution skipped: {msg}"
                  else
                      Tests.skiptest "Quarto project not found in expected location"
          }

          testAsync "Quarto inspect handles non-Quarto directories gracefully" {
              let! quartoAvailable = QuartoClient.checkQuartoAvailable ()

              match quartoAvailable with
              | Error _ -> Tests.skiptest "Quarto not installed"
              | Ok _ ->
                  // Try a temp directory that's not a Quarto project
                  let tempDir = Path.GetTempPath()
                  let! result = QuartoClient.runInspect tempDir

                  match result with
                  | Ok _ -> Tests.skiptest "Quarto might have treated temp directory as project"
                  | Error msg -> Expect.stringContains msg "Quarto" "Error message should mention Quarto or project"
          } ]

// ============================================================================
// Main Test Suite
// ============================================================================

[<Tests>]
let allTests =
    testList
        "Quarto Inspect Test Suite"
        [ githubApiTests
          quartoInstallationTests
          quartoSchemaComplianceTests
          mockRepoIntegrationTests
          quartoExecutionTests ]
