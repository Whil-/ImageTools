module Tests

open Xunit
open ImageTools
open System.IO
open System.Management.Automation

let rec directoryCopy srcPath dstPath copySubDirs =

    if not <| System.IO.Directory.Exists(srcPath) then
        let msg = System.String.Format("Source directory does not exist or could not be found: {0}", srcPath)
        raise (System.IO.DirectoryNotFoundException(msg))

    if not <| System.IO.Directory.Exists(dstPath) then
        System.IO.Directory.CreateDirectory(dstPath) |> ignore

    let srcDir = new System.IO.DirectoryInfo(srcPath)

    for file in srcDir.GetFiles() do
        let temppath = System.IO.Path.Combine(dstPath, file.Name)
        file.CopyTo(temppath, true) |> ignore

    if copySubDirs then
        for subdir in srcDir.GetDirectories() do
            let dstSubDir = System.IO.Path.Combine(dstPath, subdir.Name)
            directoryCopy subdir.FullName dstSubDir copySubDirs

[<Fact>]
let ``Imagefile collector works`` () =
    let inputfolder = DirectoryInfo("./Input")
    let inputFileCount = Helper.getImages inputfolder |> Array.length
    Assert.Equal(10,inputFileCount)

[<Fact>]
let ``I can move images in folder and based on the metadata move them to new folder with structure`` () =
    let tempfolder = DirectoryInfo("./Input_temp")
    directoryCopy "./Input" tempfolder.FullName true

    let outputfolder = DirectoryInfo("./TestOutput_" + System.Guid.NewGuid().ToString().Substring(0,10))
    if not outputfolder.Exists then outputfolder.Create()
    let cmd = MoveImagesCmdlet()
    try 
        cmd.fromPath <- tempfolder.FullName
        cmd.toPath <- outputfolder.FullName
        cmd.Invoke() |> Seq.cast<PSObject> |> Seq.iter ignore

        //Check that files are created
        Assert.Equal(10, outputfolder.GetFiles("*.*", SearchOption.AllDirectories) |> Array.length)

        //Check that files are deleted from source (some are not images and should still be there)
        Assert.Equal(5, tempfolder.GetFiles("*.*", SearchOption.AllDirectories) |> Array.length)

        //Check that empty folders are deleted from source
        Assert.False(Directory.Exists(tempfolder.FullName + "/A folder/Another folder"))

        //Check that some folders are created correctly
        Assert.True(Directory.Exists(outputfolder.FullName + "/Photos/2018/07/samsung_SM-N950F"))
        Assert.True(File.Exists(outputfolder.FullName + "/Photos/2018/07/samsung_SM-N950F/20180721_104422.jpg"))
        Assert.True(Directory.Exists(outputfolder.FullName + "/Photos/2018/07/SONY_ILCE-6000"))
        Assert.True(Directory.Exists(outputfolder.FullName + "/Photos/2020/12/samsung_SM-N986B"))
        Assert.True(Directory.Exists(outputfolder.FullName + "/Images/2019/03"))
    finally
        outputfolder.Delete(recursive = true)
        tempfolder.Delete(recursive = true)

[<Fact>]
let ``I can move videos in folder to new folder`` () =
    let tempfolder = DirectoryInfo("./Input_temp")
    directoryCopy "./Input" tempfolder.FullName true

    let outputfolder = DirectoryInfo("./TestOutput_" + System.Guid.NewGuid().ToString().Substring(0,10))
    if not outputfolder.Exists then outputfolder.Create()
    let cmd = MoveVideosCmdlet()
    try 
        cmd.fromPath <- tempfolder.FullName
        cmd.toPath <- outputfolder.FullName
        cmd.Invoke() |> Seq.cast<PSObject> |> Seq.iter ignore

        //Check that files are created
        Assert.Equal(3, outputfolder.GetFiles("*.*", SearchOption.AllDirectories) |> Array.length)

        //Check that files are deleted from source (some are not videos and should still be there)
        Assert.Equal(12, tempfolder.GetFiles("*.*", SearchOption.AllDirectories) |> Array.length)

        //Check that empty folders are deleted from source
        Assert.False(Directory.Exists(tempfolder.FullName + "/A folder/cat folder"))

        //Check that some folders are created correctly
        Assert.True(File.Exists(outputfolder.FullName + "/Videos/2019/12/cat.gif"))
        Assert.True(Directory.Exists(outputfolder.FullName + "/Videos/2019/03"))
        
    finally
        outputfolder.Delete(recursive = true)
        tempfolder.Delete(recursive = true)

[<Fact>]
let ``I can iterate through a folder and copy images`` () =
    let inputfolder = DirectoryInfo("./Input")
    let outputfolder = DirectoryInfo("./TestOutput_" + System.Guid.NewGuid().ToString().Substring(0,10))
    if not outputfolder.Exists then outputfolder.Create()

    try
        let cmd = CopyImagesCmdlet()
        cmd.fromPath <- inputfolder.FullName
        cmd.toPath <- outputfolder.FullName
        cmd.percentageSize <- 5
        cmd.quality <- 20
        cmd.Invoke() |> Seq.cast<PSObject> |> Seq.iter ignore
            
        Assert.True(File.Exists(outputfolder.FullName + "/A folder/20180721_104422.jpg"))
    
        Assert.True(Helper.getImages inputfolder |> Array.length > 0)
        Assert.Equal(Helper.getImages inputfolder |> Array.length,
                     Helper.getImages outputfolder |> Array.length)
    finally 
        outputfolder.Delete(recursive = true)

[<Fact>]
let ``I can list deleted files in copy`` () =
    let tempfolder = DirectoryInfo("./Input_temp")
    directoryCopy "./Input" tempfolder.FullName true

    let outputfolder = DirectoryInfo("./TestOutput_" + System.Guid.NewGuid().ToString().Substring(0,10))
    if not outputfolder.Exists then outputfolder.Create()
    try
        let cmd1 = CopyImagesCmdlet()
        cmd1.fromPath <- tempfolder.FullName
        cmd1.toPath <- outputfolder.FullName
        cmd1.percentageSize <- 5
        cmd1.quality <- 20
        cmd1.Invoke() |> Seq.cast<PSObject> |> Seq.iter ignore
        
        tempfolder.GetFiles("*.*",SearchOption.AllDirectories) 
        |> Seq.filter (fun f ->  f.Name.Contains("2013-10-18.jpg") ||
                                 f.Name.Contains("20201230_112809.heic")) 
        |> Seq.iter (fun (f) -> f.Delete())
        
        let cmd2 = GetDeletedImagesCmdlet()
        cmd2.basePath <- tempfolder.FullName
        cmd2.copyPath <- outputfolder.FullName
        let result = cmd2.Invoke<string>() |> Seq.toList
        result |> List.tryFind (fun (a) -> a.Contains("2013-10-18.jpg")) |> Option.isSome |> Assert.True
        result |> List.tryFind (fun (a) -> a.Contains("20201230_112809.jpg")) |> Option.isSome |> Assert.True
        result |> List.tryFind (fun (a) -> a.Contains("20201230_112809(2).jpg")) |> Option.isSome |> Assert.False

    finally
        outputfolder.Delete(recursive = true)
        tempfolder.Delete(recursive = true)
