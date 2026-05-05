# Quick Reference

## Core Paths

- Library: `src/QuartoInspect/`
- Tests: `src/QuartoInspect.Tests/`
- Schemas:
    - `src/quarto-inspect-project-json-schema.json`
    - `src/quarto-inspect-document-json-schema.json`

## Build And Test

```bash
cd src/QuartoInspect
dotnet build

cd ../QuartoInspect.Tests
dotnet restore
dotnet test
```

## Run Build Pipeline

```bash
dotnet run --project src/Build.fsproj -- -t UpdatePublications
dotnet run --project src/Build.fsproj -- -t Test
dotnet run --project src/Build.fsproj -- -t RenderSite
```

## Key F# Modules

- `QuartoInspect.QuartoTypes`
    - `parseProjectJson : string -> Result<_, string>`
    - `parseDocumentJson : string -> Result<_, string>`

- `QuartoInspect.QuartoClient`
    - `checkQuartoAvailable : unit -> Async<Result<string, string>>`
    - `runInspect : string -> Async<Result<_, string>>`
    - `validateProjectSchema : string -> Result<_, string>`
    - `validateDocumentSchema : string -> Result<_, string>`

## Typical Snippet

```fsharp
open QuartoInspect.QuartoTypes

match parseProjectJson jsonText with
| Ok parsed -> printfn "%s" parsed.Quarto.Version
| Error msg -> eprintfn "%s" msg
```

## Troubleshooting

- If tests fail unexpectedly, run `dotnet restore` in both projects.
- If Quarto checks fail, verify with `quarto --version`.
- If schema parsing fails, validate JSON shape against files in `src/`.

## Documentation Map

- Architecture: [SCHEMA_BASED_PROVIDERS.md](SCHEMA_BASED_PROVIDERS.md)
- Deep implementation notes: [QUARTO_PROVIDER_IMPLEMENTATION.md](QUARTO_PROVIDER_IMPLEMENTATION.md)
- Historical reports: [archive/](archive)

# Run with custom options
dotnet run -- --filter "GitHub" --verbose

# Check Quarto
quarto --version
quarto inspect <path>

# Check GitHub token
echo $API_GITHUB_TOKEN

# Validate JSON
jq . < sample-project.json
```

## Next Steps

1. **Try the tests**: `cd QuartoInspect.Tests && dotnet run`
2. **Read the docs**: Start with `QuartoInspect/README.md`
3. **Explore samples**: Check `sample-project.json` and `sample-document.json`
4. **Integrate**: Use `getcomputo-pub-refactored.fsx` or adapt patterns

## Resources

- **Quarto Docs**: https://quarto.org/docs/advanced/inspect/
- **FSharp.Data**: https://fsprojects.github.io/FSharp.Data/
- **Expecto**: https://github.com/haf/expecto

---

**Implementation Date**: January 20, 2026
**Status**: ✅ Complete and ready to use
