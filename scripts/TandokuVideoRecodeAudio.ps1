param(
    [Parameter(Mandatory=$true)]
    [String]
    $InputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $OutputPath

    # TODO - add parameter for audio codec (only use custom ffmpeg build when aac)
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-films.psm1" -Scope Local

# Prerequisites:
# ffmpeg supporting aac (https://github.com/marierose147/ffmpeg_windows_exe_with_fdk_aac/releases)
# TODO - install this from GitHub to ~/.tandoku/tools
# OR create a custom scoop bucket (but this would shadow standard ffmpeg build)
$tandokuToolsPath = "~/.tandoku/tools"
$tandokuToolsFfmpeg = "$tandokuToolsPath/ffmpeg-libfdk_aac/ffmpeg.exe"
if (Test-Path $tandokuToolsFfmpeg) {
    $ffmpegCmd = $tandokuToolsFfmpeg
} else {
    RequireCommand ffmpeg
    Write-Warning "$tandokuToolsFfmpeg not found, falling back to default ffmpeg"
    $ffmpegCmd = 'ffmpeg'
}

$sourceVideos = Get-ChildItem "$InputPath/*.*" -Include (GetKnownVideoExtensions -FileMask)
if (-not $sourceVideos) {
    Write-Warning 'No videos found under input path.'
    return
}

CreateDirectoryIfNotExists $OutputPath
$OutputPath = Convert-Path $OutputPath

$targetVideos = @()
foreach ($sourceVideo in $sourceVideos) {
    $fileName = Split-Path $sourceVideo -Leaf
    $targetPath = Join-Path $OutputPath $fileName
    if (Test-Path $targetPath) {
        Write-Warning "$targetPath already exists, skipping audio recoding"
    } else {
        $ffmpegArgs = ArgsToArray -i $sourceVideo -c copy '-c:a' libfdk_aac $targetPath
        & $ffmpegCmd $ffmpegArgs
        if (Test-Path $targetPath) {
            $targetVideos += $targetPath
        }
    }
}

<#
if ($targetVideos) {
    TandokuVersionControlAdd -Path $targetVideos -Kind binary
}
#>