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
    if not outputfolder.Exists then 
        outputfolder.Create()
    let cmd = MoveImagesCmdlet()
    try 
        cmd.fromPath <- tempfolder.FullName
        cmd.toPath <- outputfolder.FullName
        cmd.Invoke() |> Seq.cast<PSObject> |> Seq.iter ignore

        //Check that files are created
        Assert.Equal(10, outputfolder.GetFiles("*.*", SearchOption.AllDirectories) |> Array.length)

        //Check that files are deleted from source (4 are not images and should still be there)
        Assert.Equal(5, tempfolder.GetFiles("*.*", SearchOption.AllDirectories) |> Array.length)

        //Check that empty folders are deleted from source
        Assert.False(Directory.Exists(tempfolder.FullName + "/A folder/Another folder"))

        //Check that some folders are created correctly
        Assert.True(Directory.Exists(outputfolder.FullName + "/2018/07/samsung_SM-N950F"))
        Assert.True(File.Exists(outputfolder.FullName + "/2018/07/samsung_SM-N950F/20180721_104422.jpg"))
        Assert.True(Directory.Exists(outputfolder.FullName + "/2018/07/SONY_ILCE-6000"))
        Assert.True(Directory.Exists(outputfolder.FullName + "/2020/12/samsung_SM-N986B"))
        Assert.True(Directory.Exists(outputfolder.FullName + "/2019/03/Unknown Camera"))
    finally
        outputfolder.Delete(recursive = true)
        tempfolder.Delete(recursive = true)

[<Fact>]
let ``I can move videos in folder to new folder`` () =
    let tempfolder = DirectoryInfo("./Input_temp")
    directoryCopy "./Input" tempfolder.FullName true

    let outputfolder = DirectoryInfo("./TestOutput_" + System.Guid.NewGuid().ToString().Substring(0,10))
    if not outputfolder.Exists then 
        outputfolder.Create()
    let cmd = MoveVideosCmdlet()
    try 
        cmd.fromPath <- tempfolder.FullName
        cmd.toPath <- outputfolder.FullName
        cmd.Invoke() |> Seq.cast<PSObject> |> Seq.iter ignore

        //Check that files are created
        Assert.Equal(3, outputfolder.GetFiles("*.*", SearchOption.AllDirectories) |> Array.length)

        //Check that files are deleted from source (12 are not videos and should still be there)
        Assert.Equal(12, tempfolder.GetFiles("*.*", SearchOption.AllDirectories) |> Array.length)

        //Check that empty folders are deleted from source
        Assert.False(Directory.Exists(tempfolder.FullName + "/A folder/cat folder"))

        //Check that some folders are created correctly
        Assert.True(File.Exists(outputfolder.FullName + "/2019/12/cat.gif"))
        Assert.True(Directory.Exists(outputfolder.FullName + "/2019/03"))
        
    finally
        outputfolder.Delete(recursive = true)
        tempfolder.Delete(recursive = true)

[<Fact>]
let ``I can iterate through a folder and copy images`` () =
    let inputfolder = DirectoryInfo("./Input")
    
    let inputFileCount = Helper.getImages inputfolder |> Array.length
    let outputfolder = DirectoryInfo("./TestOutput_" + System.Guid.NewGuid().ToString().Substring(0,10))
    
    if not outputfolder.Exists then 
        outputfolder.Create()
        let cmd = CopyImagesCmdlet()
        
        try
            cmd.fromPath <- inputfolder.FullName
            cmd.toPath <- outputfolder.FullName
            cmd.percentageSize <- 5
            cmd.quality <- 20

            cmd.Invoke() |> Seq.cast<PSObject> |> Seq.iter ignore
            
        with
            | ex -> printfn "%s" (ex.Message)

        let outputFileCount = Helper.getImages outputfolder |> Array.length
        Assert.True(File.Exists(outputfolder.FullName + "/A folder/20180721_104422.jpg"))
        outputfolder.Delete(recursive = true)

        Assert.True(inputFileCount > 0)
        Assert.Equal(inputFileCount,outputFileCount)
    else
        Assert.True(false)
