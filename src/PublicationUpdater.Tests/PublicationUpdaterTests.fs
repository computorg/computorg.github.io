module PublicationUpdaterTests

open System
open System.Text.Json
open Expecto
open PublicationUpdater

let private parseJsonElement (json: string) =
    use doc = JsonDocument.Parse(json)
    doc.RootElement.Clone()

[<Tests>]
let tests =
    testList
        "PublicationUpdater"
        [ testCase "normalizeDraftValue uses lowercase booleans"
          <| fun _ ->
              Expect.equal (Generator.normalizeDraftValue "True") "true" "True should normalize to true"
              Expect.equal (Generator.normalizeDraftValue "FALSE") "false" "FALSE should normalize to false"
              Expect.equal (Generator.normalizeDraftValue "") "false" "empty should default to false"

          testCase "buildRssTitle appends authors"
          <| fun _ ->
              Expect.equal
                  (Generator.buildRssTitle "Paper" "Alice and Bob")
                  "Paper - Alice and Bob"
                  "authors should be appended"

              Expect.equal (Generator.buildRssTitle "Paper" "") "Paper" "empty authors should keep title"

          testCase "getAuthorsFromJson formats author object arrays"
          <| fun _ ->
              let metadata =
                  parseJsonElement
                      """
            [
              { "name": "Alice" },
              { "name": "Bob" },
              { "name": "Charlie" }
            ]
            """

              Expect.equal
                  (Generator.getAuthorsFromJson (Some metadata))
                  "Alice, Bob and Charlie"
                  "author objects should be formatted as a human-readable list"

          testCase "getAuthorsFromJson handles single string author"
          <| fun _ ->
              let metadata = parseJsonElement "\"Alice\""

              Expect.equal
                  (Generator.getAuthorsFromJson (Some metadata))
                  "Alice"
                  "single string author should be returned directly"

          testCase "extractCitationForRepoName uses root metadata when present"
          <| fun _ ->
              let metadata =
                  parseJsonElement
                      """
            {
              "title": "Root title",
              "authors": ["Alice"]
            }
            """

              match Generator.extractCitationForRepoName metadata "demo-repo" with
              | Ok result ->
                  let title = result.GetProperty("title").GetString()
                  Expect.equal title "Root title" "root metadata should be selected first"
              | Error msg -> failtestf "expected citation extraction to succeed, got: %s" msg

          testCase "extractCitationForRepoName falls back to config metadata"
          <| fun _ ->
              let metadata =
                  parseJsonElement
                      """
            {
              "config": {
                "metadata": {
                  "title": "Config title",
                  "authors": ["Alice"]
                }
              }
            }
            """

              match Generator.extractCitationForRepoName metadata "demo-repo" with
              | Ok result ->
                  let title = result.GetProperty("title").GetString()
                  Expect.equal title "Config title" "config metadata should be used when root metadata is absent"
              | Error msg -> failtestf "expected citation extraction to succeed, got: %s" msg

          testCase "extractCitationForRepoName prefers index.qmd in fileInformation"
          <| fun _ ->
              let metadata =
                  parseJsonElement
                      """
            {
              "fileInformation": {
                "about.qmd": {
                  "metadata": {
                    "title": "About title"
                  }
                },
                "index.qmd": {
                  "metadata": {
                    "title": "Index title"
                  }
                }
              }
            }
            """

              match Generator.extractCitationForRepoName metadata "demo-repo" with
              | Ok result ->
                  let title = result.GetProperty("title").GetString()
                  Expect.equal title "Index title" "index.qmd metadata should be preferred over other files"
              | Error msg -> failtestf "expected citation extraction to succeed, got: %s" msg

          testCase "extractCitationForRepoName returns error when no metadata exists"
          <| fun _ ->
              let metadata =
                  parseJsonElement
                      """
            {
              "quarto": { "version": "1.0.0" },
              "files": { "input": [] }
            }
            """

              match Generator.extractCitationForRepoName metadata "demo-repo" with
              | Ok _ -> failtest "expected citation extraction to fail when no metadata fields are present"
              | Error msg -> Expect.stringContains msg "No metadata found" "error should explain missing metadata"

          testCase "extractCitationForRepoName errors when fileInformation has no usable metadata"
          <| fun _ ->
              let metadata =
                  parseJsonElement
                      """
            {
              "fileInformation": {
                "about.qmd": {
                  "something": "else"
                }
              }
            }
            """

              match Generator.extractCitationForRepoName metadata "demo-repo" with
              | Ok _ -> failtest "expected citation extraction to fail when fileInformation has no metadata"
              | Error msg ->
                  Expect.stringContains
                      msg
                      "No metadata found in fileInformation"
                      "error should mention missing metadata in fileInformation"

          testCase "serializeToYaml preserves lowercase draft values"
          <| fun _ ->
              let publication: Generator.Publication =
                  { title = "Paper"
                    name = "paper-repo"
                    authors = "Alice and Bob"
                    journal = "Computo"
                    doi = "10.0000/example"
                    year = 2025
                    date = "2025-01-02"
                    description = "Description"
                    ``abstract`` = "Abstract"
                    repo = "paper-repo"
                    bibtex = "@article{paper}"
                    pdf = "paper.pdf"
                    url = "https://example.test/paper"
                    draft = "false" }

              let yaml = Generator.serializeToYaml [ publication ]
              Expect.stringContains yaml "draft: false" "draft should stay lowercase in YAML output"
              Expect.stringContains yaml "title: Paper" "serialized YAML should contain the title"

          testCase "formatRssDate formats valid input as RFC1123"
          <| fun _ ->
              let formatted = Generator.formatRssDate "2025-01-02"
              Expect.stringContains formatted "2025" "formatted RSS date should contain the year"
              Expect.stringContains formatted "GMT" "formatted RSS date should be RFC1123-like"

          testCase "formatRssDate falls back on invalid input"
          <| fun _ ->
              let formatted = Generator.formatRssDate "not-a-date"
              Expect.isGreaterThan formatted.Length 0 "fallback date should still be a non-empty string"
              Expect.stringContains formatted "GMT" "fallback should still be RFC1123-like" ]
