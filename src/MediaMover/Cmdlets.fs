namespace ImageTools

open System.Management.Automation
open System.IO

type ImageMoverData = {
    DatePath:string
    Camera:string
    ImageName:string
    SourcePath:string
    TargetBasePath:string
} with member this.NewFullName = this.TargetBasePath + @"\" + this.DatePath + @"\" + this.Camera + @"\" + this.ImageName

type VideoMoverData = {
    DatePath:string
    VideoName:string
    SourcePath:string
    TargetBasePath:string
} with member this.NewFullName = this.TargetBasePath + @"\" + this.DatePath + @"\" + this.VideoName

// Ref: https://github.com/drewnoakes/metadata-extractor-dotnet
[<Cmdlet("Get","FileAssortedProperties")>]
type GetFileAssortedPropertiesCmdlet() =
    inherit Cmdlet()
    [<Parameter(Mandatory = true, Position = 0)>]
    member val filePaths : string[] = null with get, set

    override this.ProcessRecord() =
        for file in this.filePaths do
            let fromDir = FileInfo file
            let metadata = Helper.extractInfo(fromDir.FullName)
            this.WriteObject(metadata)

[<Cmdlet("Move","Images")>]
type MoveImagesCmdlet() =
    inherit Cmdlet()

    [<Parameter(Mandatory = true, Position = 0)>]
    member val fromPath : string = null with get, set

    [<Parameter(Mandatory = true, Position = 1)>]
    member val toPath : string = null with get, set

    override this.ProcessRecord() =
        let fromDir = DirectoryInfo this.fromPath
        let files = Helper.getImages fromDir
        let toDir = DirectoryInfo this.toPath

        let mutable index = 0
        let progress = ProgressRecord(0,"Move images","Moving...")
        let items = Array.length files

        while index < items do
            progress.PercentComplete <- (index * 100) / items
            progress.CurrentOperation <- index.ToString() + " / " + items.ToString() + " files completed"
            let parallellcnt = 8
            this.WriteProgress(progress)
            
            files
            |> Array.skip index
            |> Array.take (min (items - index) parallellcnt)
            |> Array.map (fun file ->
                async {
                    let raw = Helper.extractInfo file.FullName
                    let ImageData = { 
                        DatePath = match raw.CaptureTime with
                                   | Some t -> t.ToString("yyyy") + @"\" + t.ToString("MM")
                                   | None -> file.LastWriteTime.ToString("yyyy") + @"\" + file.LastWriteTime.ToString("MM")
                        Camera = match (raw.Make, raw.Model) with
                                 | (Some a, Some b) -> $"{a}_{b}"
                                 | (Some a, None) -> $"{a}"
                                 | (None, Some b) -> $"{b}"
                                 | (None, None) -> "Unknown camera"
                        ImageName = file.Name
                        SourcePath = file.DirectoryName
                        TargetBasePath = toDir.FullName
                    }
                    this.WriteVerbose $"Moving... ({file.FullName} -> {ImageData.NewFullName})"
                    Helper.MoveFile file ImageData.NewFullName |> ignore
                    
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
            index <- index + parallellcnt

        progress.PercentComplete <- 100
        progress.CurrentOperation <- "Moving complete, deleting empty directories..."
        Helper.cleanEmptyDirectoriesInPath fromDir
        this.WriteProgress(progress)

[<Cmdlet("Move","Videos")>]
type MoveVideosCmdlet() =
    inherit Cmdlet()

    [<Parameter(Mandatory = true, Position = 0)>]
    member val fromPath : string = null with get, set

    [<Parameter(Mandatory = true, Position = 1)>]
    member val toPath : string = null with get, set

    override this.ProcessRecord() =
        let fromDir = DirectoryInfo this.fromPath
        let files = Helper.getVideos fromDir
        let toDir = DirectoryInfo this.toPath

        let mutable index = 0
        let progress = ProgressRecord(0,"Move videos","Moving...")
        let items = Array.length files

        while index < items do
            progress.PercentComplete <- (index * 100) / items
            progress.CurrentOperation <- index.ToString() + " / " + items.ToString() + " files completed"
            let parallellcnt = 8
            this.WriteProgress(progress)
            
            files
            |> Array.skip index
            |> Array.take (min (items - index) parallellcnt)
            |> Array.map (fun file ->
                async {
                    let VideoData = { 
                        DatePath = file.LastWriteTime.ToString("yyyy") + @"\" + file.LastWriteTime.ToString("MM")
                        VideoName = file.Name
                        SourcePath = file.DirectoryName
                        TargetBasePath = toDir.FullName
                    }
                    this.WriteVerbose $"Moving... ({file.FullName} -> {VideoData.NewFullName})"
                    Helper.MoveFile file VideoData.NewFullName |> ignore
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
            index <- index + parallellcnt

        progress.PercentComplete <- 100
        progress.CurrentOperation <- "Moving complete, deleting empty directories..."
        Helper.cleanEmptyDirectoriesInPath fromDir
        this.WriteProgress(progress)

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
        let files = Helper.getImages fromDir
        let toDir = DirectoryInfo this.toPath

        //Iterate through the files in batches and process each batch in parallell
        let mutable index = 0
        let parallellcnt = 8
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
                    Helper.copyImageToJPGIfNotExist
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
    inherit Cmdlet()

    [<Parameter(Mandatory = true, Position = 0)>]
    member val basePath : string = null with get, set

    [<Parameter(Mandatory = true, Position = 1)>]
    member val copyPath : string = null with get, set

    override a.ProcessRecord() =
        let baseDir = DirectoryInfo a.basePath
        let copyDir = DirectoryInfo a.copyPath
        let baseFiles = Helper.getImages baseDir
        let copyFiles = copyDir.GetFiles("*.jpg", SearchOption.AllDirectories)

        let stripFileNameFromBaseAndExtension (file:FileInfo) (dir:string) : string =
            file.DirectoryName.Replace(dir, "") + @"\" + Path.GetFileNameWithoutExtension(file.Name)

        let strippedBaseFileNames = baseFiles
                                    |> Array.map (fun file ->
                                        stripFileNameFromBaseAndExtension file baseDir.FullName)

        let strippedCopyFileNames = copyFiles
                                    |> Array.map (fun file ->
                                        stripFileNameFromBaseAndExtension file copyDir.FullName)

        let difference = (Set.ofArray strippedCopyFileNames) - (Set.ofArray strippedBaseFileNames)
        difference
        |> Seq.iter (fun item -> a.WriteObject(copyDir.FullName + item + ".jpg"))

