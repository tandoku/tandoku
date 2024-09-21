param(
    [Parameter(Mandatory=$true)]
    [String]
    $InputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $OutputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $ReferencePath,

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
        $baseName = GetSubtitleBaseName $fileName

        # prefer subtitle reference if present
        $referenceFilePath = Get-Item "$ReferencePath/$baseName.*" -Include (GetKnownSubtitleExtensions -FileMask) -ErrorAction SilentlyContinue
        if (-not $referenceFilePath) {
            # otherwise use video reference (alass will extract audio stream for alignment)
            $referenceFilePath = Get-Item "$ReferencePath/$baseName.*" -Include (GetKnownVideoExtensions -FileMask)
        }
        $alassArgs = ArgsToArray $referenceFilePath $sourceSubtitle $targetPath
        if ($NoFpsGuessing) {
            $alassArgs += '--disable-fps-guessing'
        }
        if ($NoSplit) {
            $alassArgs += '--no-split'
        }
        & 'alass' $alassArgs
        if (Test-Path $targetPath) {
            $targetSubtitles += $targetPath
        }
    }
}

if ($targetSubtitles) {
    TandokuVersionControlAdd -Path $targetSubtitles -Kind text
}
