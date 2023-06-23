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

$meta = TandokuCalibreExtractMeta.ps1 -Path $Path

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
    Write-Error $tandokuVolumeNewOut
    return
}

Write-Host "Created new volume at $volumePath"

tandoku source import (Join-Path $Path '*.opf') --volume $volumePath
tandoku source import (Join-Path $Path 'cover.jpg') --volume $volumePath
tandoku source import (Join-Path $Path '*.azw3') -n "$title.azw3" --volume $volumePath

# ./TandokuKindleStoreExtractMeta.ps1 -Asin $asin -OutFile "$volumePath/source/kindle-metadata.xml" -KindleStoreMetadataPath $KindleStoreMetadataPath

# ./TandokuVolumeSetCover.ps1 "$volumePath/source/cover.jpg" -VolumePath $volumePath

# TODO - these should probably be part of 'tandoku build' later?
# ./TandokuKindleUnpack.ps1 "$volumePath/source/*.azw3" -Destination "$volumePath/temp/ebook"
# ./TandokuImagesImport.ps1 "$volumePath/temp/ebook/mobi7/Images"