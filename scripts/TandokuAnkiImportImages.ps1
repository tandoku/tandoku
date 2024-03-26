param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter(Mandatory=$true)]
    [String]
    $TempDestination,

    [Parameter()]
    [String]
    $VolumePath
)

$media = Get-Content "$path/media" | ConvertFrom-Json -AsHashtable

if (-not (Test-Path $TempDestination)) {
    [void] (New-Item $TempDestination -ItemType Directory)
}

foreach ($mediaItem in $media.Keys) {
    $fileName = $media[$mediaItem]
    $fileExt = Split-Path $fileName -Extension
    if (($fileExt -eq '.jpg') -or ($fileExt -eq '.jpeg')) {
        Copy-Item "$path/$mediaItem" "$TempDestination/$fileName"
    }
}

TandokuImagesImport -Path $TempDestination -VolumePath $volumePath