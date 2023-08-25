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

$meta = TandokuCalibreExtractMeta -Path $Path
if (-not $meta) {
    return
}

$volumeInfo = tandoku_volume_new -Title $meta.title -Moniker $Moniker -Tags $Tags
if (-not $volumeInfo) {
    return
}
$volumePath = $volumeInfo.VolumePath

tandoku source import $metadataPath --volume $volumePath
tandoku source import $coverPath --volume $volumePath
tandoku source import $azwPath -n source.azw3 --volume $volumePath

TandokuKindleStoreExtractMeta -Asin $meta.asin -OutFile "$volumePath/source/kindle-metadata.xml" -KindleStoreMetadataPath $KindleStoreMetadataPath

TandokuVolumeSetCover -Path "$volumePath/source/cover.jpg" -VolumePath $volumePath

TandokuCalibreImportMeta -VolumePath $volumePath

# TODO: add files to source control (specify text/binary)

# TODO: these should probably be part of 'tandoku build' later?
TandokuKindleUnpack -Path "$volumePath/source/source.azw3" -Destination "$volumePath/temp/mobi"
TandokuImagesImport -Path "$volumePath/temp/mobi/mobi8/OEBPS/Images/" -VolumePath $volumePath

# TODO: add -Commit switch to commit to source control?