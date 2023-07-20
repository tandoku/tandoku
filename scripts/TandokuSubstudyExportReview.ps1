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
    $Destination,

    [Parameter()]
    [String]
    $Combine

    # TODO: support volume
    # [Parameter()]
    # [String]
    # $VolumePath
)

# Prerequisites:
# scoop install rust
# cargo install substudy
# (or install substudy binary to PATH from https://github.com/emk/subtitles-rs/releases)

function GetSubtitleItem($baseName, $lang) {
    $filter = "$baseName.$lang*.srt"
    $item = Get-ChildItem $SubtitlePath -Filter $filter
    if (-not $item) {
        Write-Warning "Could not find $filter, skipping"
        return
    }
    return $item
}

if (-not $Language) {
    $Language = 'ja'
}
if (-not $ReferenceLanguage) {
    $ReferenceLanguage = 'en'
}

# TODO: get $Destination from ~/.tandoku/config.yaml if not specified (defaults to ~/.tandoku/staging/substudy/)

# TODO: common code for listing videos
$items = @()
Get-ChildItem $Path -Filter *.mp4 |
    ForEach-Object {
        $baseName = Split-Path $_ -LeafBase
        $sub1 = GetSubtitleItem $baseName $Language
        $sub2 = GetSubtitleItem $baseName $ReferenceLanguage

        Push-Location $Destination
        substudy export review $_ $sub1 $sub2

        $reviewPath = Convert-Path "$($baseName)_review"
        $indexPath = Join-Path $reviewPath 'index.html'
        $html = ConvertFrom-Html -Path $indexPath
        $html.SelectNodes('html/body/div/img[@class="play-button"]') |
            ForEach-Object { $_.Remove() }
        $html.SelectNodes('html/body/div/audio') |
            ForEach-Object { $_.Remove() }

        $ebookIndexPath = Join-Path $reviewPath 'ebook-index.html'
        Set-Content $ebookIndexPath $html.OuterHtml

        if ($Combine) {
            $items += @{
                Path = $ebookIndexPath
                RelativePath = (Resolve-Path $ebookIndexPath -Relative).Replace('\','/')
                Title = $baseName
            }
        } else {
            $epubPath = "$reviewPath.epub"
            ebook-convert $ebookIndexPath $epubPath --authors "substudy" --language $Language
        }

        # TODO: when volume specified, substudy export should go to <volume>/temp/substudy while ebook-convert should go to staging

        Pop-Location
    }

if ($Combine) {
    $html = ConvertFrom-Html @"
<html>
    <head>
        <meta charset="UTF-8">
        <title>$Combine</title>
    </head>
    <body>
        <h1>$Combine</h1>
        <p></p>
    </body>
</html>'
"@
    $inner = @()
    foreach ($i in $items) {
        $inner += "<li><a href='$($i.RelativePath)'>$($i.Title)</a></li>"
    }
    $html.SelectSingleNode('html/body/p').InnerHtml = $inner -join [Environment]::NewLine

    $rootIndexPath = Join-Path $Destination "$Combine.html"
    Set-Content $rootIndexPath $html.OuterHtml

    $epubPath = Join-Path $Destination "$Combine.epub"
    ebook-convert $rootIndexPath $epubPath --authors "substudy" --language $Language
}