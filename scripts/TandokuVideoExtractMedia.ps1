param(
    [Parameter(Mandatory=$true)]
    [String]
    $InputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $OutputPath,

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-films.psm1" -Scope Local

# Prerequisites:
# scoop install python
# pip install subs2cia
# TODO - fork subs2cia repo and merge PR that removes pandas and PR that makes --batch ignore directory
# (in the meantime - git clone https://github.com/benjaminhottell/subs2cia and pip install in that directory)
RequireCommand subs2cia

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}
$volumePath = $Volume.Path
$volumeLanguage = $Volume.Definition.Language

$subtitleFiles = Get-ChildItem "$InputPath/*.*" -Include (GetKnownSubtitleExtensions -FileMask)

$items = @()

foreach ($subtitleFile in $subtitleFiles) {
    $baseName = Split-Path $subtitleFile -LeafBase
    $targetPath = "$OutputPath/$baseName"
    if (Test-Path "$targetPath/$baseName.tsv") {
        Write-Warning "Media for $baseName already exists in $targetPath, skipping extraction"
    } else {
        CreateDirectoryIfNotExists $targetPath -Clobber
        $videoFile = Get-Item "$volumePath/video/$baseName.*" -Include (GetKnownVideoExtensions -FileMask)
        if (-not $videoFile) {
            Write-Warning "Video for $baseName not found, skipping extraction"
        } else {
            $subs2ciaArgs = ArgsToArray srs --inputs $videoFile $subtitleFile `
                --output-dir $targetPath `
                --ignore-none `
                --target-language $volumeLanguage `
                --bitrate 160
            & 'subs2cia' $subs2ciaArgs

            $items += Get-Item $targetPath
        }
    }
}

if ($items) {
    TandokuVersionControlAdd -Path $OutputPath -Kind binary
}
