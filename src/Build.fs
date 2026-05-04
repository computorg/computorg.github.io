open System.IO
open Fake.Core
open Fake.DotNet

let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))

let buildOptions (options: DotNet.Options) =
    { options with
        WorkingDirectory = repoRoot }

let fakeArgs =
    System.Environment.GetCommandLineArgs() |> Array.skip 1 |> Array.toList

Context.FakeExecutionContext.Create false "Build.fs" fakeArgs
|> Context.RuntimeContext.Fake
|> Context.setExecutionContext

let ensureExitCode exitCode context =
    if exitCode <> 0 then
        failwithf "%s failed with exit code %d" context exitCode

Target.create "UpdatePublications" (fun _ ->
    let result =
        DotNet.exec buildOptions "run" "--project src/PublicationUpdater.Cli/PublicationUpdater.Cli.fsproj -- ."

    ensureExitCode result.ExitCode "Publication updater")

Target.create "Test" (fun _ ->
    let publicationUpdaterTests =
        DotNet.exec buildOptions "test" "src/PublicationUpdater.Tests/PublicationUpdater.Tests.fsproj"

    ensureExitCode publicationUpdaterTests.ExitCode "PublicationUpdater tests"

    let quartoInspectTests =
        DotNet.exec buildOptions "test" "src/QuartoInspect.Tests/QuartoInspect.Tests.fsproj"

    ensureExitCode quartoInspectTests.ExitCode "QuartoInspect tests")

Target.create "RenderSite" (fun _ ->
    let result =
        CreateProcess.fromRawCommand "quarto" [ "render" ]
        |> CreateProcess.withWorkingDirectory repoRoot
        |> Proc.run

    ensureExitCode result.ExitCode "quarto render")

Target.create "Default" ignore

open Fake.Core.TargetOperators

"UpdatePublications" ==> "RenderSite" ==> "Default" |> ignore

Target.runOrDefaultWithArguments "Default"
