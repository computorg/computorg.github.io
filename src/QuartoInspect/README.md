# Quarto Inspect Type Provider & Tests

This directory contains a refactored F# infrastructure for working with Quarto inspect output, leveraging FSharp.Data's JSON type provider for compile-time type safety.

## Project Structure

### QuartoInspect/
Core library providing:
- **QuartoTypes.fs**: Type definitions and JSON type providers
  - `QuartoProjectProvider`: Type provider for project-level inspect output (based on `quarto-inspect-project-json-schema.json` using `Schema=` mode)
  - `QuartoDocumentProvider`: Type provider for document-level inspect output (based on `quarto-inspect-document-json-schema.json` using `Schema=` mode)
  - Helper functions for parsing and validation

- **QuartoClient.fs**: Client for executing Quarto commands
  - `runInspect`: Execute `quarto inspect` on a given path
  - `checkQuartoAvailable`: Verify Quarto installation
  - `validateDocumentSchema`: Validate JSON against document schema
  - `validateProjectSchema`: Validate JSON against project schema

### QuartoInspect.Tests/
Comprehensive test suite using Expecto framework:
- **GitHub API Availability Tests**: Verify GitHub API connectivity and authentication
- **Quarto Installation Tests**: Check Quarto availability
- **Schema Compliance Tests**: Validate JSON against schemas
- **Mock Repository Integration**: Test with actual computorg repositories
- **Quarto Inspect Execution**: Integration tests with real Quarto commands

## Building

### Build the library:
```bash
cd src/QuartoInspect
dotnet build
```

### Build and run tests:
```bash
cd src/QuartoInspect.Tests
dotnet restore
dotnet build
dotnet run
```

### Run specific test categories:
```bash
# Run only GitHub API tests
dotnet run -- --filter "GitHub API"

# Run only schema compliance tests
dotnet run -- --filter "Schema Compliance"

# Run with verbose output
dotnet run -- --verbose
```

## Usage in Your Scripts

### Using the Type Providers

```fsharp
#r "nuget: FSharp.Data"
#load "../QuartoInspect/QuartoTypes.fs"
open QuartoInspect.QuartoTypes

// Parse and validate JSON
let jsonStr = System.IO.File.ReadAllText("output.json")
match parseProjectJson jsonStr with
| Ok parsed -> 
    printfn "Version: %s" parsed.Quarto.Version
    printfn "Directory: %s" parsed.Dir
| Error msg -> printfn "Error: %s" msg
```

### Using the Quarto Client

```fsharp
#r "nuget: FSharp.Data"
#load "../QuartoInspect/QuartoClient.fs"
open QuartoInspect.QuartoClient

// Check Quarto availability
let! version = QuartoClient.checkQuartoAvailable()
match version with
| Ok v -> printfn "Quarto version: %s" v
| Error msg -> printfn "Error: %s" msg

// Run quarto inspect
let! result = QuartoClient.runInspect "/path/to/project"
match result with
| Ok inspectResult ->
    printfn "Execution time: %A" inspectResult.executionTime
    // Validate schema
    match QuartoClient.validateProjectSchema inspectResult.jsonContent with
    | Ok json -> printfn "Valid project schema"
    | Error msg -> printfn "Schema error: %s" msg
| Error msg -> printfn "Inspect failed: %s" msg
```

## Type Providers

The type providers use FSharp.Data's **JSON Schema mode** with the official Quarto JSON schemas:
- **quarto-inspect-project-json-schema.json**: Official project schema
- **quarto-inspect-document-json-schema.json**: Official document schema

The type providers are declared with `Schema=` syntax, which validates JSON against the actual JSON Schema specification rather than inferring from samples.

### Using JSON Schema Mode
```fsharp
// Direct schema validation via type provider
type QuartoProjectProvider = JsonProvider<Schema="quarto-inspect-project-json-schema.json">
```

This approach:
- Validates against official JSON Schema specification
- Provides compile-time type safety based on schema constraints
- Ensures conformance to Quarto's published interface
- **Compile-time type safety**: Errors caught at compile time, not runtime
- **IntelliSense support**: Full IDE support in VS Code and Visual Studio
- **Schema validation**: Ensures conformance to Quarto specifications
- **Type inference**: F# infers types from JSON structure

### Project Schema Fields:
- `quarto`: Version information
- `dir`: Project directory path
- `engines`: List of rendering engines (python, r, julia, etc.)
- `config`: Project configuration metadata
- `files`: Input, resource, and config files
- `fileInformation`: Per-document metadata including code cells
- `extensions`: Installed extensions

### Document Schema Fields:
- `quarto`: Version information
- `engines`: List of rendering engines
- `formats`: Available output formats
- `resources`: Resource files
- `fileInformation`: Document metadata including code cells
- `project`: (Optional) Parent project info if document is in a project

## Testing

### Test Categories

1. **GitHub API Availability**
   - Verifies GitHub API connectivity
   - Tests repository retrieval
   - Handles rate limiting gracefully

2. **Quarto Installation**
   - Checks if Quarto is installed and accessible
   - Retrieves Quarto version

3. **Schema Compliance**
   - Validates sample JSON against schemas
   - Tests type provider parsing
   - Ensures required fields presence

4. **Mock Repository Integration**
   - Tests with `published-paper-example` repository
   - Verifies repository structure
   - Skips gracefully if repository not found

5. **Quarto Inspect Execution**
   - Runs actual `quarto inspect` commands
   - Validates output schema compliance
   - Tests error handling for non-Quarto directories

### Running Tests

Tests use Expecto and support various options:

```bash
# Run all tests
dotnet run

# Run with specific parallelism
dotnet run -- --parallel 2

# List available tests
dotnet run -- --list-tests

# Run tests matching a pattern
dotnet run -- --filter "GitHub"
```

## Environment Setup

### Required:
- **.NET 8.0** or later
- **Quarto** (for inspect command tests)
- **GitHub API Token** (optional, set via `API_GITHUB_TOKEN` environment variable for authenticated requests)

### Environment Variables:
```bash
# For authenticated GitHub API requests
export API_GITHUB_TOKEN="ghp_your_token_here"
```

## Integration with getcomputo-pub.fsx

A refactored version of the main script is provided as `getcomputo-pub-refactored.fsx`. It includes:

- Embedded type providers for compile-time validation
- Improved error handling with schema validation
- Better separation of concerns
- Cleaner JSON parsing using type providers

To use the refactored version:
```bash
dotnet fsi src/getcomputo-pub-refactored.fsx
```

## Schema Files

The JSON schemas are included in the repository:
- `quarto-inspect-document-json-schema.json`: Document inspection schema
- `quarto-inspect-project-json-schema.json`: Project inspection schema

These match the official schemas from https://quarto.org/docs/advanced/inspect/.

## Extending the Type Providers

To add support for new fields returned by Quarto inspect:

1. Update the official Quarto JSON Schema files (or wait for Quarto to update them)
2. Rebuild the project - type provider automatically updates to match schema
3. New fields are now available with full IntelliSense support

The type provider uses JSON Schema mode, so it strictly validates against the schema specification.

## Error Handling

The library uses `Result<'T, string>` for error handling, making error cases explicit:

```fsharp
let validate (jsonStr: string) =
    match QuartoClient.validateProjectSchema jsonStr with
    | Ok element -> printfn "Valid"
    | Error msg -> printfn "Invalid: %s" msg
```

## Performance Considerations

- Type providers are evaluated at compile time, so there's no runtime overhead
- JSON parsing is fast due to FSharp.Data's optimized implementation
- Tests run in parallel (4 workers by default)
- GitHub API calls may be rate-limited (~60 req/hour unauthenticated, ~5000/hour with token)

## Troubleshooting

### "Quarto not available"
- Ensure Quarto is installed: `quarto --version`
- Verify Quarto is in PATH
- Tests will skip gracefully if Quarto is not available

### "GitHub API rate limit exceeded"
- Set GitHub API token: `export API_GITHUB_TOKEN="..."`
- This increases limit from 60 to 5000 requests per hour

### "Mock repository not found"
- Tests skip gracefully if `published-paper-example` doesn't exist
- This is expected in development environments

### Schema validation errors
- Check that JSON matches Quarto's official schemas
- Review error message for missing required fields
- Validate raw JSON: `jq . < output.json`
