# Schema-Based Type Providers Implementation

This document explains how the type providers use FSharp.Data's JSON Schema validation mode with the official Quarto JSON schemas.

## FSharp.Data JSON Provider Modes

FSharp.Data's JsonProvider supports two distinct modes:

### 1. Sample Mode (Type Inference)
```fsharp
type MyProvider = JsonProvider<"sample.json">
```
- Infers types from actual JSON sample data
- Types are based on what's in the sample
- Good for exploring JSON structure

### 2. Schema Mode (Schema Validation)
```fsharp
type MyProvider = JsonProvider<Schema="schema.json">
```
- Validates against JSON Schema specification
- Types are based on schema constraints
- Provides strict validation and better IDE support
- **This is what we use** ✅

## Official Quarto Schemas

The project uses two official JSON schemas published by Quarto:

### Project Schema
**File**: `src/quarto-inspect-project-json-schema.json`

Source: https://quarto.org/docs/advanced/inspect/

Defines the structure returned by `quarto inspect <PROJECT_PATH>`

**Key validated fields**:
- `quarto.version` - String, Quarto version
- `dir` - String, project directory path
- `engines` - Array of strings, rendering engines
- `config` - Object, project configuration
- `files` - Object with input, resources, configResources, config arrays
- `fileInformation` - Object with per-document metadata
- `extensions` - Array of extension objects

### Document Schema
**File**: `src/quarto-inspect-document-json-schema.json`

Source: https://quarto.org/docs/advanced/inspect/

Defines the structure returned by `quarto inspect <DOCUMENT_PATH>`

**Key validated fields**:
- `quarto.version` - String, Quarto version
- `engines` - Array of strings, rendering engines
- `formats` - Object, output formats
- `resources` - Array of strings, resource files
- `fileInformation` - Object with document metadata
- `project` - (Optional) parent project information

## Type Provider Declaration

In `QuartoTypes.fs`, the type providers use `Schema=` mode:

```fsharp
/// Uses JSON Schema validation directly
type QuartoProjectProvider = 
    JsonProvider<Schema="src/quarto-inspect-project-json-schema.json">

type QuartoDocumentProvider = 
    JsonProvider<Schema="src/quarto-inspect-document-json-schema.json">
```

## How Schema Mode Works

1. **Schema Definition**: JSON Schema file specifies allowed structure
2. **Type Generation**: FSharp.Data generates F# types matching schema constraints
3. **Compile-time Validation**: Invalid JSON detected at compile time
4. **Runtime Parsing**: JSON is parsed with validation against schema

Example schema excerpt:
```json
{
  "type": "object",
  "properties": {
    "quarto": {
      "type": "object",
      "properties": {
        "version": { "type": "string" }
      }
    },
    "dir": { "type": "string" },
    "engines": {
      "type": "array",
      "items": { "type": "string" }
    }
  }
}
```

Generated F# types would be:
```fsharp
parsed.Quarto.Version : string
parsed.Dir : string
parsed.Engines : string[]
```

## Validation Chain

```
Official JSON Schema (Quarto publishes)
    ↓
FSharp.Data Type Provider (Schema= mode)
    ↓
Compile-time Type Generation
    ↓
F# Strongly-Typed Access
    ↓
Runtime Validation via ParseAsync
```

## Benefits of Schema Mode

| Aspect | Sample Mode | Schema Mode |
|--------|------------|------------|
| Type Source | JSON sample data | JSON Schema spec |
| Validation | Inferred from sample | Schema specification |
| Strictness | Loose (sample-dependent) | Strict (schema-enforced) |
| IDE Support | Good (sample-based) | Excellent (schema-defined) |
| Unknown Fields | Accepted | Rejected |
| Type Safety | Good | Excellent |
| Updates | Require sample update | Schema change only |

## Sample Files as Documentation

While we use `Schema=` mode, we maintain sample JSON files for documentation:
- **sample-project.json** - Valid example of project inspect output
- **sample-document.json** - Valid example of document inspect output

These serve as:
- Documentation of schema structure
- Test data for integration tests
- Examples for developers
- Validation that schemas match reality

## Type Provider Usage

```fsharp
// Parse with schema validation
let parseProjectJson (jsonStr: string) : Result<QuartoProjectProvider, string> =
    try
        Ok (QuartoProjectProvider.Parse(jsonStr))
    with ex ->
        Error $"Schema validation failed: {ex.Message}"

// Type-safe access with IntelliSense
let parsed = QuartoProjectProvider.Parse(jsonStr)
let version = parsed.Quarto.Version    // String, validated by schema
let engines = parsed.Engines           // string[], validated by schema
let files = parsed.Files.Input         // string[], validated by schema
```

## Updating for Quarto Schema Changes

When Quarto updates their schemas:

1. Update the schema files locally:
   - `src/quarto-inspect-project-json-schema.json`
   - `src/quarto-inspect-document-json-schema.json`

2. Rebuild the project:
   ```bash
   cd QuartoInspect
   dotnet clean
   dotnet build
   ```

3. Type provider automatically generates new types
4. Compiler shows any incompatibilities
5. All new fields are available with IntelliSense

## Differences from Sample Mode

### Schema Mode (What We Use)
```fsharp
type QuartoProjectProvider = 
    JsonProvider<Schema="src/quarto-inspect-project-json-schema.json">

// Type safety based on schema specification
let parsed = QuartoProjectProvider.Parse(jsonStr)
let version = parsed.Quarto.Version  // Validated against schema
```

### Sample Mode (Alternative)
```fsharp
type QuartoProjectProvider = 
    JsonProvider<"sample-project.json">

// Type safety based on sample structure
let parsed = QuartoProjectProvider.Parse(jsonStr)
let version = parsed.Quarto.Version  // Validated against sample
```

**Key Difference**: Schema mode validates against the JSON Schema specification, which is more authoritative and comprehensive than relying on a single sample.

## Reference Materials

- **Quarto Inspect**: https://quarto.org/docs/advanced/inspect/
- **FSharp.Data JSON Schema**: https://fsprojects.github.io/FSharp.Data/library/JsonSchema.html
- **JSON Schema**: https://json-schema.org/
- **FSharp.Data Documentation**: https://fsprojects.github.io/FSharp.Data/

## Summary

The implementation uses **FSharp.Data's JSON Schema validation mode** (`Schema=` syntax) with the official Quarto JSON schemas. This provides:

✅ Type safety based on official specifications  
✅ Strict validation against JSON Schema  
✅ Excellent IDE IntelliSense support  
✅ Clear compile-time error messages  
✅ Direct alignment with Quarto's published interface  
✅ Automatic updates when Quarto updates schemas

