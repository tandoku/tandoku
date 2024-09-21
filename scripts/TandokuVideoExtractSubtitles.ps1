param(
    [Parameter(Mandatory=$true)]
    [String]
    $InputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $OutputPath,

    [Parameter()]
    [String]
    $Language
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-films.psm1" -Scope Local

# Prerequisites:
# scoop install ffmpeg
RequireCommand ffmpeg

$sourceVideos = Get-ChildItem "$InputPath/*.*" -Include (GetKnownVideoExtensions -FileMask)
if (-not $sourceVideos) {
    Write-Warning 'No videos found under input path.'
    return
}

CreateDirectoryIfNotExists $OutputPath

$targetSubtitles = @()
foreach ($sourceVideo in $sourceVideos) {
    $fileName = Split-Path $sourceVideo -LeafBase
    $fileName = "$fileName.srt"
    $targetPath = Join-Path $OutputPath $fileName
    if (Test-Path $targetPath) {
        Write-Warning "$targetPath already exists, skipping subtitle extraction"
    } else {
        $ffmpegArgs = ArgsToArray -i $sourceVideo
        if ($Language) {
            $langCode = [CultureInfo]::GetCultureInfo($Language).ThreeLetterISOLanguageName
            $ffmpegArgs += ArgsToArray -map "0:m:language:$langCode"
        } else {
            $ffmpegArgs += ArgsToArray -map 0:s:0
        }
        $ffmpegArgs += $targetPath
        & 'ffmpeg' $ffmpegArgs
        if (Test-Path $targetPath) {
            $targetSubtitles += $targetPath
        }
    }
}

if ($targetSubtitles) {
    TandokuVersionControlAdd -Path $targetSubtitles -Kind text
}
