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
    # also add TandokuVolumeSet.ps1 which adds to version control
    if ($meta.title) {
        tandoku volume set title $meta.title --volume $volumePath
    }
}