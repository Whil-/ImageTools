module ImageTools.Helper 

open System.IO
open ImageMagick
open System.Text.RegularExpressions
open MetadataExtractor

type FileAssortedProperties = {
    PerceivedType: string
    DateTaken: string
    CameraModel: string
    Name: string
    Path: string
}

type private Metadata = { Type:int; Name:string; Description:string }
type Info = { Make:Option<string>; Model:Option<string>; CaptureTime:Option<System.DateTime> }

let extractInfo (path:string) =
    let sometags = ImageMetadataReader.ReadMetadata(path)
                   |> Seq.tryPick (fun (item) -> 
                      if item.Name = "Exif IFD0" then
                          Some item.Tags
                      else None)
                   |> Option.map (Seq.choose (fun (tag) -> 
                        // Check all tags with the following:
                        // ImageMetadataReader.ReadMetadata(path) |> 
                        match tag.Type with
                        | 271 | 272 | 306 -> Some { Type = tag.Type; Name = tag.Name; Description = tag.Description }
                        | _ -> None))
    if sometags.IsSome then
        let tags = sometags.Value
        {
            Make = Seq.tryPick (fun (tag) -> if tag.Name = "Make" then Some tag.Description else None) tags
            Model = Seq.tryPick (fun (tag) -> if tag.Name = "Model" then Some tag.Description else None) tags
            CaptureTime = Seq.tryPick (fun (tag) -> if tag.Name = "Date/Time" then 
                                                        try
                                                            let culture = System.Globalization.CultureInfo.InvariantCulture
                                                            Some (System.DateTime.ParseExact(tag.Description, 
                                                                                             "yyyy:MM:dd HH:mm:ss",
                                                                                             culture))
                                                        with
                                                        | _ -> None
                                                    else None) tags
        }
    else
        { Make = None; Model = None; CaptureTime = None }

let GetMaybeUpdatedProfile (image:MagickImage) =
    let mutable profile = image.GetExifProfile()
    if isNull profile then
        profile <- ExifProfile()

    let dto = profile.GetValue(ExifTag.DateTimeOriginal)
    if isNull dto then
        profile.SetValue(ExifTag.DateTimeOriginal,"1970:01:01 12:00:00")
        profile.SetValue(ExifTag.DateTimeDigitized,"1970:01:01 12:00:00")
        profile.SetValue(ExifTag.DateTime,"1970:01:01 12:00:00")
    profile

let MoveFile (oldImage:FileInfo) (newFullName:string) =
    let newImage = FileInfo(newFullName)
    if not newImage.Directory.Exists then
        newImage.Directory.Create()
    try
        oldImage.CopyTo(newImage.FullName) |> ignore
        oldImage.Delete()
    with
    | ex -> printfn $"{ex.Message}"

let rec cleanEmptyDirectoriesInPath (path:DirectoryInfo) =
    // Unless empty, run the function again on each subdirectory
    let subdirectories = path.GetDirectories("*")
    if subdirectories.Length > 0 then
          subdirectories 
          |> Array.iter cleanEmptyDirectoriesInPath
    
    if (0, 0) = (path.GetDirectories("*").Length, path.GetFiles().Length) then
        path.Delete()

let copyImageToJPGIfNotExist (file:FileInfo) (oldBasePath:string) (newBasePath:string) (percentageSize:int) (quality:int) : Unit =
    let newimage = FileInfo(file.DirectoryName.Replace(oldBasePath, newBasePath) +
                            @"\" + Path.GetFileNameWithoutExtension(file.Name) + ".jpg")
    // Check so that image doesn't already exist. If it does, do nothing.
    if not newimage.Exists then
        if not newimage.Directory.Exists then newimage.Directory.Create()
        let image = new MagickImage(file)
        image.Resize (Percentage(percentageSize))
        image.Quality <- quality

        GetMaybeUpdatedProfile image
        |> image.SetProfile

        image.Write(newimage)
        newimage.CreationTimeUtc <- file.CreationTimeUtc
        newimage.LastWriteTimeUtc <- file.LastWriteTimeUtc

let getVideos (fromDir: DirectoryInfo) =
    let extensionExpression = Regex(@".(mov|mp4|mpeg|gif|avi|mts)", RegexOptions.IgnoreCase)
    fromDir.GetFiles("*.*", SearchOption.AllDirectories)
    |> Array.filter (fun file -> extensionExpression.IsMatch file.Extension)
    
let getImages (fromDir: DirectoryInfo) =
    let extensionExpression = Regex(@".(jpg|jpeg|png|dng|arw|heic)", RegexOptions.IgnoreCase)
    fromDir.GetFiles("*.*", SearchOption.AllDirectories) 
        |> Array.filter (fun file -> extensionExpression.IsMatch file.Extension)


