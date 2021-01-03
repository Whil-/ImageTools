namespace ImageTools

open System.Management.Automation
open ImageMagick
open System.IO
open System.Text.RegularExpressions
open System.Linq

(*
open Microsoft.Win32

type FileAssortedProperties = {
    PerceivedType: string
    DateTaken: string
    CameraModel: string
    Name: string
    Path: string
}

type ImageMoverData = {
    Timestamp:System.DateTime
    CameraModel:string
    ImageName:string
    SourcePath:string
    TargetPath:string
}

// Ref(1): https://stackoverflow.com/questions/8351713/how-can-i-extract-the-date-from-the-media-created-column-of-a-video-file
// Ref(2): https://stackoverflow.com/questions/22382010/what-options-are-available-for-shell32-folder-getdetailsof
[<Cmdlet("Get","FileAssortedProperties")>]
type GetFileAssortedPropertiesCmdlet() =
    inherit Cmdlet()
    [<Parameter(Mandatory = true, Position = 0)>]
    member val fromPath : string = null with get, set

    override this.ProcessRecord() =
        let fromDir = DirectoryInfo this.fromPath
        let shell = new Shell32.ShellClass()
        let folder = shell.NameSpace(fromDir.Name)
        for fileInDir in fromDir.EnumerateFiles() do
            let file = folder.ParseName(fileInDir.Name)
            let props = {
                //Add test to testing library to check that each of these numbers are correct. References above
                // Ex. folder.GetDetailsOf(null, 9) will give the name of number 9
                PerceivedType = folder.GetDetailsOf(file, 9)
                DateTaken = folder.GetDetailsOf(file, 12)
                CameraModel = folder.GetDetailsOf(file, 30)
                Name = folder.GetDetailsOf(file, 0)
                Path = folder.GetDetailsOf(null, 194)
            }
            this.WriteObject(props)
*)

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
        let progress = ProgressRecord(0,"Copy images","Copying...")
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
