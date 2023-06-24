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

$meta = TandokuCalibreExtractMeta.ps1 -Path $Path
if (-not $meta) {
    return
}

$volumeNewArgs = @('volume', 'new', $meta.title)
if ($Moniker) {
    $volumeNewArgs += @('--moniker', $Moniker)
}
if ($Tags) {
    $volumeNewArgs += @('--tags', ($Tags -join ','))
}

# TODO: add JSON output instead of string parsing
$tandokuVolumeNewOut = (& "tandoku" $volumeNewArgs)
if ($tandokuVolumeNewOut -match ' at (.+)$') {
    $volumePath = $Matches[1]
} else {
    Write-Error "Failed to create new volume"
    Write-Error "$tandokuVolumeNewOut"
    return
}

Write-Host "Created new volume at $volumePath"

tandoku source import $metadataPath --volume $volumePath
tandoku source import $coverPath --volume $volumePath
tandoku source import $azwPath -n "$($meta.title).azw3" --volume $volumePath

TandokuKindleStoreExtractMeta.ps1 -Asin $meta.asin -OutFile "$volumePath/source/kindle-metadata.xml" -KindleStoreMetadataPath $KindleStoreMetadataPath

TandokuVolumeSetCover.ps1 "$volumePath/source/cover.jpg" -VolumePath $volumePath

# TODO: add files to source control (specify text/binary)

# TODO: set additional metadata in volume.yaml from Calibre, Kindle metadata
# (ISBN, ASIN, author, publisher, ...?)

# TODO: these should probably be part of 'tandoku build' later?
# TandokuKindleUnpack.ps1 "$volumePath/source/*.azw3" -Destination "$volumePath/temp/ebook"
# TandokuImagesImport.ps1 "$volumePath/temp/ebook/mobi7/Images"