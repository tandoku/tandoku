param(
    [Parameter(Mandatory=$true)]
    [String[]]
    $Path,

    [Parameter()]
    [String]
    $VolumePath
)

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

# Import videos
$videosToImport = Get-ChildItem $Path -Filter *.mp4
if (-not $videosToImport) {
    Write-Warning 'No videos to import found at the specified path.'
    return
}
TandokuSourceImport -Path $videosToImport -VolumePath $volumePath -VersionControl binary

# Find corresponding subtitles (tandoku source import)
# Note: import reference language subtitles into /source/ref-en/ path
