module ImageTools.Helper 

open System.IO
open ImageMagick
open System.Text.RegularExpressions
open System.Collections.Generic
open System
open System.Threading

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

let CopyImageToJPGIfNotExist (file:FileInfo) (oldBasePath:string) (newBasePath:string) (percentageSize:int) (quality:int) : Unit =
    let newimage = FileInfo(file.DirectoryName.Replace(oldBasePath, newBasePath) + @"\" + file.Name + ".jpg")
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

let GetFiles (fromDir: DirectoryInfo) = 
    let extensionExpression = Regex(@".(jpg|jpeg|png|dng|arw|heic)", RegexOptions.IgnoreCase)

    fromDir.GetFiles("*.*", SearchOption.AllDirectories) 
        |> Array.filter (fun file -> extensionExpression.IsMatch file.Extension)


