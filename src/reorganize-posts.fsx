#r "nuget: Microsoft.Extensions.FileSystemGlobbing"
#r "nuget: YamlDotNet"

open System.IO
open Microsoft.Extensions.FileSystemGlobbing
open YamlDotNet.Serialization
open System.Collections.Generic

let blogDir = "blog"

let deserializer = DeserializerBuilder().Build()

let extractDate: string -> string =
    deserializer.Deserialize<Dictionary<string, obj>>
    >> (fun x -> x["date"] :?> string)

let extractFrontMatter =
    File.ReadAllText
    >> _.Split("---\n")
    >> function
        | [| _; frontMatter; content |] -> frontMatter, content
        | _ -> "", ""

let addDatetoDirNameAndRenameIt dir (fileName: string) =
    let date = fileName |> extractFrontMatter |> fst |> extractDate
    let dirName = Path.GetRelativePath(dir, Path.GetDirectoryName(fileName))
    let newDirName = Path.Combine(dir, date + "-" + dirName)
    let oldDirName = Path.Combine(dir, dirName)
    printfn "Renaming %s to %s" oldDirName newDirName
    Directory.Move(oldDirName, newDirName)

let matcher = Matcher()
matcher.AddInclude("*/*.qmd")


blogDir
|> matcher.GetResultsInFullPath
|> Seq.iter (addDatetoDirNameAndRenameIt blogDir)


let newsDir = "news"
let serializer = SerializerBuilder().Build()


newsDir
|> matcher.GetResultsInFullPath
|> Seq.map (
    extractFrontMatter
    >> (fun (f, c) -> [ ("date", extractDate f); ("description", c) ] |> Map)
)
|> Seq.sortBy (fun x -> x.["date"])
|> serializer.Serialize
|> (fun n -> File.WriteAllText(Path.Combine("site", "news.yml"), n))
