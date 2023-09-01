param(
    [Parameter(Mandatory=$true)]
    [String]
    $Property,

    [Parameter(Mandatory=$true)]
    [String]
    $Value,

    [Parameter()]
    [String]
    $VolumePath
)

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

tandoku volume set $Property $Value --volume $volumePath

# TODO: `tandoku volume set` should return affected files rather than hard-coding this
TandokuVersionControlAdd $volume.definitionPath -Kind text