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
    $ReferenceSubtitlePath,

    [Parameter()]
    [String]
    $ReferenceLanguage,

    [Parameter()]
    [String]
    $Destination

    # TODO: support volume
    # [Parameter()]
    # [String]
    # $VolumePath
)

# Prerequisites:
# scoop install rust
# cargo install substudy
# (or install substudy binary to PATH from https://github.com/emk/subtitles-rs/releases)

if (-not $Language) {
    $Language = 'ja'
}
if (-not $ReferenceLanguage) {
    $ReferenceLanguage = 'en'
}

# TODO: get $Destination from ~/.tandoku/config.yaml if not specified (defaults to ~/.tandoku/staging/substudy/)

# TODO: common code for listing videos
Get-ChildItem $Path -Filter *.mp4 |
    ForEach-Object {
        $baseName = Split-Path $_ -LeafBase
        $sub1Filter = "$baseName.$Language*.srt"
        $sub1 = Get-ChildItem $SubtitlePath -Filter $sub1Filter
        if (-not $sub1) {
            Write-Warning "Could not find $sub1Filter, skipping"
            return
        }

        # TODO: factor out function
        $sub2Filter = "$baseName.$ReferenceLanguage*.srt"
        $sub2 = Get-ChildItem $SubtitlePath -Filter $sub2Filter
        if (-not $sub2) {
            Write-Warning "Could not find $sub2Filter, skipping"
            return
        }

        Push-Location $Destination
        substudy export review $_ $sub1 $sub2

        # TODO: replace play.svg, delete audio, ebook-convert .\index.html <target>.epub --authors "substudy" --language $Language
        # also, substudy export should go to <volume>/temp/substudy while ebook-convert should go to staging

        Pop-Location
    }