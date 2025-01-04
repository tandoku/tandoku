param(
    [Parameter(Mandatory=$true)]
    [String]
    $Path,

    [Parameter()]
    [String]
    $VolumePath,

    [Parameter()]
    [Switch]
    $UseReading # TODO - make this the default (invert to $PlainText)
)

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$apkgPath = (Get-Item $Path)
if (-not $apkgPath) {
    Write-Error "Missing .apkg file at $Path"
    return
}

# TODO - read Anki notes using anki python module rather than requiring txt export
# see https://addon-docs.ankiweb.net/command-line-use.html

$parentPath = (Split-Path $apkgPath -Parent)
$apkgBaseName = (Split-Path $apkgPath -LeafBase)
$txtPath = (Get-Item "$parentPath/$apkgBaseName.txt")
if (-not $txtPath) {
    Write-Error "Missing $apkgBaseName.txt file at $Path"
    return
}

$sourceApkg = TandokuVolumeSourceAdd -Path $apkgPath -VersionControl binary -VolumePath $volumePath
$sourceTxt = TandokuVolumeSourceAdd -Path $txtPath -VersionControl text -VolumePath $volumePath

$tempApkgPath = "$volumePath/temp/apkg"
Expand-Archive -LiteralPath $sourceApkg -Destination $tempApkgPath

TandokuAnkiImportImages -Path $tempApkgPath -TempDestination "$tempApkgPath/images" -VolumePath $volumePath

TandokuAnkiGenerateContent -Path $sourceTxt -VolumePath $volumePath -UseReading:$UseReading
