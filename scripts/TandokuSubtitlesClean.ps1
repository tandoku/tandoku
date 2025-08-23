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

# prerequisites:
# scoop install SubtitleEdit
# Note that SubtitleEdit is Windows-only / netfx but look into
# https://github.com/SubtitleEdit/subtitleedit-cli
# for cross-platform net6+ cli when needed
# OR use libse library from tandoku cli
RequireCommand SubtitleEdit

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}

# TODO - add function to module for getting subtitles in directory (optionally with language specified)
$sourceSubtitles = Get-ChildItem "$InputPath/*.*" -Include (GetKnownSubtitleExtensions -FileMask -TtmlOnly)
if ($sourceSubtitles) {
    CreateDirectoryIfNotExists $OutputPath

    tandoku subtitles ttml-to-webvtt $InputPath $OutputPath

    # TODO - implement MergeSameTexts natively as tandoku command over WebVTT files
    # (tandoku subtitles clean - can call ttml-to-webvtt internally if needed)
    SubtitleEdit /convert *.* WebVTT `
        /inputFolder:$OutputPath `
        /outputFolder:$OutputPath `
        /overwrite `
        /MergeSameTexts |
        Write-Output

    $targetSubtitles = Get-ChildItem "$OutputPath/*.vtt"

    # Revert SubtitleEdit corruption of <rt> tags
    $targetSubtitles |
        ReplaceStringInFiles '&lt;rt&gt;' '<rt>'

    if ($targetSubtitles) {
        TandokuVersionControlAdd -Path $targetSubtitles -Kind text
    }
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
# TODO - more robust removal of .ass drawing mode content (/deletecontains below)
SubtitleEdit /convert *.* AdvancedSubStationAlpha `
    /inputFolder:$InputPath `
    /outputFolder:$OutputPath `
    /overwrite `
    /deletecontains:"\p1" `
    /RemoveUnicodeControlChars `
    /RemoveFormatting `
    /MergeSameTexts |
    Write-Output
$targetSubtitles = Get-ChildItem "$OutputPath/*.ass"

if ($targetSubtitles) {
    # Additional cleanup
    $targetSubtitles |
        ReplaceStringInFiles '&(lrm|rlm);' ''

    TandokuVersionControlAdd -Path $targetSubtitles -Kind text
}