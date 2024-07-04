param(
    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$path = "$VolumePath/images"
$imageExtensions = GetImageExtensions

$inputItems = @()
foreach ($imageExtension in $imageExtensions) {
    $inputItems += Get-ChildItem -Path $path -Filter "*$imageExtension"
}

# TODO - pass imageExtensions to python script
$outputItems = python $PSScriptRoot/python/TandokuImagesAnalyze_easyocr.py $path $volume.definition.language

if ($outputItems) {
    TandokuVersionControlAdd -Path $path -Kind binary
}