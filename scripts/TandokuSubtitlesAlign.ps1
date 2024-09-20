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
    [Switch]
    $NoFpsGuessing,

    [Parameter()]
    [Switch]
    $NoSplit,

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-films.psm1" -Scope Local

# prerequisites:
# scoop install alass
RequireCommand alass

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

$targetSubtitles = @()
foreach ($sourceSubtitle in $sourceSubtitles) {
    $fileName = Split-Path $sourceSubtitle -Leaf
    $targetPath = Join-Path $OutputPath $fileName
    if (Test-Path $targetPath) {
        Write-Warning "$targetPath already exists, skipping subtitle alignment"
    } else {
        $videoFilePath = GetVideoForSubtitle $fileName $VideoPath
        $args = @($videoFilePath,$sourceSubtitle,$targetPath)
        if ($NoFpsGuessing) {
            $args += '--disable-fps-guessing'
        }
        if ($NoSplit) {
            $args += '--no-split'
        }
        & 'alass' $args
        if (Test-Path $targetPath) {
            $targetSubtitles += $targetPath
        }
    }
}

if ($targetSubtitles) {
    TandokuVersionControlAdd -Path $targetSubtitles -Kind text
}
