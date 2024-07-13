param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

RequireCommand mutool

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$pdfPath = Get-Item $Path
if (-not $pdfPath) {
    Write-Error "Specified path does not exist: $Path"
    return
}

$sourcePdf = TandokuSourceImport -Path $pdfPath -VersionControl binary -VolumePath $volumePath

$tempImagesPath = "$volumePath/temp/pdf"
CreateDirectoryIfNotExists $tempImagesPath -Clobber
Push-Location $tempImagesPath
mutool extract $sourcePdf
Pop-Location

TandokuImagesImport -Path $tempImagesPath -VolumePath $volumePath