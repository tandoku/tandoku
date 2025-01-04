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

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

$media = Get-Content "$path/media" | ConvertFrom-Json -AsHashtable
$imageExtensions = GetImageExtensions

CreateDirectoryIfNotExists $TempDestination

foreach ($mediaItem in $media.Keys) {
    $fileName = $media[$mediaItem]
    $fileExt = Split-Path $fileName -Extension
    if ($imageExtensions -contains $fileExt) {
        Copy-Item "$path/$mediaItem" "$TempDestination/$fileName"
    }
}

TandokuImagesAdd -Path $TempDestination -VolumePath $volumePath