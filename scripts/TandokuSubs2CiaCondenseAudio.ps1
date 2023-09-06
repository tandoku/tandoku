param(
    [Parameter()]
    [String]
    $Path,

    [Parameter()]
    [String]
    $SubtitlePath,

    [Parameter()]
    [String]
    $Language,

    [Parameter()]
    [String]
    $Destination

    # TODO: support volume
    # [Parameter()]
    # [String]
    # $VolumePath
)

# Prerequisites:
# scoop install python
# pip install subs2cia

# TODO: share this with TandokuSubstudyExportReview.ps1
function GetSubtitleItem($subPath, $baseName, $lang) {
    $filter = "$baseName.$lang*.srt"
    $item = Get-ChildItem $subPath -Filter $filter
    if (-not $item) {
        Write-Warning "Could not find $filter, skipping"
        return
    }
    return $item
}

if (-not $Language) {
    $Language = 'ja'
}
if (-not $SubtitlePath) {
    $SubtitlePath = $Path
}
if (-not $Destination) {
    $Destination = '.'
}

# TODO: get $Destination from ~/.tandoku/config.yaml if not specified (defaults to ~/.tandoku/staging/subs2cia/)

# TODO: common code for listing videos
Get-ChildItem $Path -Filter *.mp4 |
    ForEach-Object {
        $baseName = Split-Path $_ -LeafBase
        $sub = GetSubtitleItem $SubtitlePath $baseName $Language

        # Note: subs2cia condense has default logic for skipping non-dialogue lines which is occasionally overaggressive;
        # eventually should use a 'cleaned' subtitle derived from tandoku content and turn off this logic (--ignore-none)
        subs2cia condense -i $_ $sub -d $Destination --target-language $Language

        # TODO: when volume specified, subs2cia condense should go to <volume>/temp/subs2cia/condense or directly to staging?
    }