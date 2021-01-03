namespace ImageTools

open System.Management.Automation
open ImageMagick
open System.IO
open System.Text.RegularExpressions
open System.Linq

[<Cmdlet("Copy","Images")>]
type CopyImagesCmdlet() =
    inherit Cmdlet()

    [<Parameter(Mandatory = true, Position = 0)>]
    member val fromPath : string = null with get, set

    [<Parameter(Mandatory = true, Position = 1)>]
    member val toPath : string = null with get, set

    [<Parameter>]
    member val percentageSize : int = 30 with get, set

    [<Parameter>]
    member val quality : int = 75 with get, set

    override this.ProcessRecord() =
        let fromDir = DirectoryInfo this.fromPath
        let files = Helper.GetFiles fromDir
        let toDir = DirectoryInfo this.toPath

        //Iterate through the files in batches and process each batch in parallell
        let mutable index = 0
        let parallellcnt = 4
        let items = Array.length files
        let progress = new ProgressRecord(0,"Copy images","Copying...")
        while index < items do
            progress.PercentComplete <- (index * 100) / items
            progress.CurrentOperation <- index.ToString() + " / " + items.ToString() + " files completed" 
            this.WriteProgress(progress)
            files
            |> Array.skip index
            |> Array.take (min (items - index) parallellcnt)
            |> Array.map (fun file ->
                async {
                    Helper.CopyImageToJPGIfNotExist
                                    file
                                    fromDir.FullName
                                    toDir.FullName
                                    this.percentageSize
                                    this.quality
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
            index <- index + parallellcnt
        progress.PercentComplete <- 100
        this.WriteProgress(progress)


[<Cmdlet("Get","DeletedImages")>]
type GetDeletedImagesCmdlet() =
    inherit PSCmdlet()

    [<Parameter(Mandatory = true, Position = 0)>]
    member val basePath : string = null with get, set

    [<Parameter(Mandatory = true, Position = 1)>]
    member val copyPath : string = null with get, set

    override this.ProcessRecord() =
        let baseDir = DirectoryInfo this.basePath
        let copyDir = DirectoryInfo this.copyPath
        let baseFiles = Helper.GetFiles baseDir
        let copyFiles = copyDir.GetFiles("*.jpg", SearchOption.AllDirectories) 

        let stripBaseFileName (file:FileInfo) (dir:string) : string =
            file.DirectoryName.Replace(dir, "") + @"\" + file.Name

        let stripCopyFileName (file:FileInfo) (dir:string) : string =
            file.DirectoryName.Replace(dir, "") + @"\" + file.Name.Substring(0,file.Name.Length - ".jpg".Length) 

        let strippedBaseFileNames = baseFiles 
                                    |> Array.map (fun file -> 
                                        stripBaseFileName file baseDir.FullName)

        let strippedCopyFileNames = copyFiles
                                    |> Array.map (fun file -> 
                                        stripCopyFileName file copyDir.FullName)

        let difference = (Set.ofArray strippedCopyFileNames) - (Set.ofArray strippedBaseFileNames)
        difference
        |> Seq.iter (fun item -> this.WriteObject(copyDir.FullName + item + ".jpg"))
                    