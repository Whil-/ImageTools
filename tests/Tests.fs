module Tests

open Xunit
open ImageTools
open System.IO
open System.Management.Automation

[<Fact>]
let ``I can iterate through a folder and copy files`` () =

    let inputfolder = DirectoryInfo("./Input")
    
    let inputFileCount = Helper.GetFiles inputfolder |> Array.length
    let outputfolder = DirectoryInfo("TestOutput_" + System.Guid.NewGuid().ToString().Substring(0,10))
    
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

        let outputFileCount = Helper.GetFiles outputfolder |> Array.length
        outputfolder.Delete(recursive = true)

        Assert.Equal(inputFileCount,outputFileCount)
    else
        Assert.True(false)
