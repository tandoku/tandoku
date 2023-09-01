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

foreach ($p in $Path) {
    $meta = TandokuCalibreExtractMeta -Path $p

    # TODO: generalize this to just set each property?
    if ($meta.title) {
        TandokuVolumeSet -Property title -Value $meta.title -VolumePath $volumePath
    }
}