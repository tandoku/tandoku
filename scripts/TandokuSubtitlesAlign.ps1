param(
    [Parameter(Mandatory=$true)]
    [String]
    $InputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $OutputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $VideoPath,

    [Parameter()]
    $Volume
)

# prerequisites:
# scoop install alass

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-films.psm1" -Scope Local

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}

$sourceSubtitles = Get-ChildItem "$InputPath/*.*" -Include (GetKnownSubtitleExtensions -FileMask)
if (-not $sourceSubtitles) {
    Write-Warning 'No subtitles found under input path.'
    return
}

CreateDirectoryIfNotExists $OutputPath
# TODO - rewrite as foreach and let alass return its output
$targetSubtitles = $sourceSubtitles | ForEach-Object {
    $fileName = Split-Path $_ -Leaf
    $targetPath = Join-Path $OutputPath $fileName
    $videoFilePath = GetVideoForSubtitle $fileName $VideoPath
    $alassOutput = alass $videoFilePath $_ $targetPath
    if (Test-Path $targetPath) {
        $targetPath
    }
}

if ($targetSubtitles) {
    TandokuVersionControlAdd -Path $targetSubtitles -Kind text
}