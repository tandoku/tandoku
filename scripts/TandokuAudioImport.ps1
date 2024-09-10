param(
    [Parameter(Mandatory=$true)]
    [String[]]
    $Path,

    [Parameter()]
    [String]
    $Destination,

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}
$volumePath = $Volume.Path
$audioPath = "$volumePath/audio"

if (-not $Destination) {
    $Destination = $audioPath
}
# TODO - verify that $Destination is under $audioPath
# (at least when using version control)

CreateDirectoryIfNotExists $Destination
$audioExtensionMasks = GetKnownAudioExtensions -FileMask
$items = @()
foreach ($audioExtensionMask in $audioExtensionMasks) {
    $items += CopyItemIfNewer -Path $Path -Filter $audioExtensionMask -Destination $Destination -PassThru
}

if ($items) {
    TandokuVersionControlAdd -Path $audioPath -Kind binary
}
