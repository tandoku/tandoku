param(
    [Parameter(Mandatory=$true)]
    [String]
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

$apkgPath = (Get-Item $Path)
$parentPath = (Split-Path $apkgPath -Parent)
$apkgBaseName = (Split-Path $apkgPath -LeafBase)
$txtPath = (Get-Item "$parentPath/$apkgBaseName.txt")

if (-not $apkgPath) {
    Write-Error "Missing .apkg file at $Path"
    return
} elseif (-not $txtPath) {
    Write-Error "Missing $apkgBaseName.txt file at $Path"
    return
}

$sourceApkg = TandokuSourceImport -Path $apkgPath -VersionControl binary -VolumePath $volumePath
$sourceTxt = TandokuSourceImport -Path $txtPath -VersionControl text -VolumePath $volumePath

$tempApkgPath = "$volumePath/temp/apkg"
Expand-Archive -LiteralPath $sourceApkg -Destination $tempApkgPath

TandokuAnkiImportImages -Path $tempApkgPath -TempDestination "$tempApkgPath/images" -VolumePath $volumePath

TandokuAnkiGenerateContent -Path $sourceTxt -VolumePath $volumePath