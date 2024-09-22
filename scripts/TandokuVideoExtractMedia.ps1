param(
    [Parameter(Mandatory=$true)]
    [String]
    $InputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $OutputPath,

    [Parameter()]
    [int]
    $AudioPadding,

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
$tempPath = "$volumePath/temp"

# TODO - keep intermediate files under non-temp volume paths
# (should simplify integration with workflow deps for incremental build,
#  and dvc will de-dupe the physical files via hash)

$tempSubtitlesPath = "$tempPath/subtitles"
CreateDirectoryIfNotExists $tempSubtitlesPath -Clobber

tandoku subtitles generate $InputPath $tempSubtitlesPath

$subtitleFiles = Get-ChildItem "$tempSubtitlesPath/*.*" -Include (GetKnownSubtitleExtensions -FileMask)

$tempMediaPath = "$tempPath/subs2cia-srs"
CreateDirectoryIfNotExists $tempMediaPath

foreach ($subtitleFile in $subtitleFiles) {
    $baseName = Split-Path $subtitleFile -LeafBase
    if (Test-Path "$tempMediaPath/$baseName.tsv") {
        Write-Warning "Media for $baseName already exists in $tempMediaPath, skipping extraction"
    } else {
        $videoFile = Get-Item "$volumePath/video/$baseName.*" -Include (GetKnownVideoExtensions -FileMask)
        if (-not $videoFile) {
            Write-Warning "Video for $baseName not found, skipping extraction"
        } else {
            $subs2ciaArgs = ArgsToArray srs --inputs $videoFile $subtitleFile `
                --output-dir $tempMediaPath `
                --ignore-none `
                --target-language $volumeLanguage `
                --bitrate 160
            if ($AudioPadding) {
                $subs2ciaArgs += ArgsToArray --padding $AudioPadding
            }
            & 'subs2cia' $subs2ciaArgs
        }
    }
}

# TODO - consider moving media into subfolders by content base name
# (separate option to import-subs2cia-media; cannot use --image|audio-prefix since it varies by file)
$importMediaArgs = ArgsToArray content transform import-subs2cia-media $InputPath $OutputPath `
    --media-path $tempMediaPath `
    --audio-prefix clips/
if ($AudioPadding) {
    $importMediaArgs += ArgsToArray --audio-padding $AudioPadding
}
$media = InvokeTandokuCommand $importMediaArgs
if ($media.images) {
    TandokuImagesImport $media.images -VolumePath $volumePath
}
if ($media.audio) {
    TandokuAudioImport $media.audio "$volumePath/audio/clips" -Volume $volume
}
