param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $Moniker,

    [Parameter()]
    [String[]]
    $Tags,

    [Parameter()]
    [String]
    $KindleStoreMetadataPath
)

$metadataPath = (Get-Item "$Path/metadata.opf")
$coverPath = (Get-Item "$Path/cover.jpg")
$azwPath = (Get-Item "$Path/*.azw3")

if (-not $metadataPath) {
    Write-Error "Missing metadata.opf file at $Path"
    return
} elseif (-not $coverPath) {
    Write-Error "Missing cover.jpg file at $Path"
    return
} elseif ($azwPath.Count -ne 1) {
    Write-Error "Expecting single .azw3 file at $Path"
    return
}

$volumeInfo = TandokuVolumeNew -Moniker $Moniker -Tags $Tags
if (-not $volumeInfo) {
    return
}
$volumePath = $volumeInfo.VolumePath

$metadata = TandokuCalibreExtractMeta $metadataPath
$sourceBookName = $metadata.asin ?? 'source'

$sourceMetadata = TandokuSourceImport -Path $metadataPath -VersionControl text -VolumePath $volumePath
$sourceCover = TandokuSourceImport -Path $coverPath -VersionControl binary -VolumePath $volumePath
$sourceBook = TandokuSourceImport -Path $azwPath -FileName "$sourceBookName.azw3" -VersionControl binary -VolumePath $volumePath

TandokuVolumeSetCover -Path $sourceCover -VolumePath $volumePath

TandokuKindleUnpack -Path $sourceBook -Destination "$volumePath/temp/mobi"

TandokuCalibreImportMeta -Path $sourceMetadata,"$volumePath/temp/mobi/mobi8/OEBPS/content.opf" -VolumePath $volumePath

$renameResult = TandokuVolumeRename -VolumePath $volumePath
if (-not $renameResult) {
    Write-Error 'Volume rename failed, aborting'
    return
}
$volumePath = $renameResult.RenamedPath

# TODO: check for comic/picture book type before doing this
TandokuImagesImport -Path "$volumePath/temp/mobi/mobi8/OEBPS/Images/" -VolumePath $volumePath

# TODO: add -Commit switch to commit to source control?
Write-Host "TODO: git commit, git push, dvc push"
