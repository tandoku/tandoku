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

# prerequisites:
# scoop install SubtitleEdit
# Note that SubtitleEdit is Windows-only / netfx but look into
# https://github.com/SubtitleEdit/subtitleedit-cli
# for cross-platform net6+ cli when needed

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-films.psm1" -Scope Local

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}

# TODO - add function to module for getting subtitles in directory (optionally with language specified)
$sourceSubtitles = Get-ChildItem "$InputPath/*.*" -Include (GetKnownSubtitleExtensions -FileMask)
if (-not $sourceSubtitles) {
    Write-Warning 'No subtitles found under input path.'
    return
}

CreateDirectoryIfNotExists $OutputPath

# SubtitleEdit is a GUI application so | Write-Output is used to wait for completion
# (PowerShell does not wait for GUI applications to finish by default)
# NOTE - using SubRip for now instead of AdvancedSubStationAlpha due to
# https://github.com/SubtitleEdit/subtitleedit/issues/8707
SubtitleEdit /convert *.* SubRip `
    /inputFolder:$InputPath `
    /outputFolder:$OutputPath `
    /overwrite `
    /RemoveFormatting `
    /RemoveUnicodeControlChars |
    Write-Output

$targetSubtitles = Get-ChildItem "$OutputPath/*.srt"
if ($targetSubtitles) {
    TandokuVersionControlAdd -Path $targetSubtitles -Kind text
}