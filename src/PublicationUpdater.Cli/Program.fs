open System
open System.IO
open PublicationUpdater

[<EntryPoint>]
let main argv =
    let rootDir =
        if argv.Length > 0 then
            argv.[0]
        else
            Directory.GetCurrentDirectory()

    Generator.run rootDir
