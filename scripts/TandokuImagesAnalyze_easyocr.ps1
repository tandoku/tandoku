param(
    [Parameter()]
    [String[]]
    $Path,

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}

# TODO - pass inputItems to python script
# $inputItems = Get-Item -Path $Path
python3 $PSScriptRoot/python/TandokuImagesAnalyze_easyocr.py "$($Volume.Path)/images" $Volume.Definition.Language