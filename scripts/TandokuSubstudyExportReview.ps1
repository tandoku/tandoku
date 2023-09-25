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
    $Combine,

    [Parameter()]
    [Switch]
    $NoAudio

    # TODO: support volume
    # [Parameter()]
    # [String]
    # $VolumePath
)

# Prerequisites:
# scoop install rust
# cargo install substudy
# (or install substudy binary to PATH from https://github.com/emk/subtitles-rs/releases)
# Install-Module powerhtml

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
if (-not $ReferenceLanguage) {
    $ReferenceLanguage = 'en'
}
if (-not $ReferenceSubtitlePath) {
    $ReferenceSubtitlePath = $SubtitlePath
}

$audioTag = ($NoAudio ? '-noaudio' : '')

# TODO: get $Destination from ~/.tandoku/config.yaml if not specified (defaults to ~/.tandoku/staging/substudy/)

# TODO: common code for listing videos
$items = @()
Get-ChildItem $Path -Filter *.mp4 |
    ForEach-Object {
        $baseName = Split-Path $_ -LeafBase
        $sub1 = GetSubtitleItem $SubtitlePath $baseName $Language
        $sub2 = GetSubtitleItem $ReferenceSubtitlePath $baseName $ReferenceLanguage

        Push-Location $Destination
        substudy export review $_ $sub1 $sub2

        $reviewPath = Convert-Path "$($baseName)_review"
        $indexPath = Join-Path $reviewPath 'index.html'
        $html = ConvertFrom-Html -Path $indexPath

        $html.SelectSingleNode('html/head/link[@href="style.css"]').Attributes['href'].Value = 'ebook-style.css'

        $html.SelectNodes('html/body/div/img[@class="play-button"]') |
            ForEach-Object { $_.Remove() }

        if ($NoAudio) {
            $html.SelectNodes('html/body/div/audio') |
                ForEach-Object { $_.Remove() }
        }

        $ebookIndexPath = Join-Path $reviewPath "ebook-index$audioTag.html"
        Set-Content $ebookIndexPath $html.OuterHtml

        $ebookStylePath = Join-Path $reviewPath 'ebook-style.css'
        Set-Content $ebookStylePath @'
.subtitle .text p {
  margin: 0 0 15px 0;
}

.subtitle .native {
  font-style: italic;
  font-size: 10pt;
}
'@

        if ($Combine) {
            $items += @{
                Path = $ebookIndexPath
                RelativePath = (Resolve-Path $ebookIndexPath -Relative).Replace('\','/')
                Title = $baseName
            }
        } else {
            $epubPath = "$reviewPath$audioTag.epub"
            ebook-convert $ebookIndexPath $epubPath --authors "substudy" --language $Language
        }

        # TODO: when volume specified, substudy export should go to <volume>/temp/substudy while ebook-convert should go to staging

        Pop-Location
    }

if ($Combine) {
    $html = ConvertFrom-Html @"
<html xmlns="http://www.w3.org/1999/xhtml">
    <head>
        <title>$Combine$audioTag</title>
    </head>
    <body>
        <h1>$Combine</h1>
        <p></p>
    </body>
</html>
"@
    $inner = @()
    foreach ($i in $items) {
        $inner += "<li><a href='$([System.Web.HttpUtility]::HtmlAttributeEncode($i.RelativePath))'>$($i.Title)</a></li>"
    }
    $html.SelectSingleNode('html/body/p').InnerHtml = $inner -join [Environment]::NewLine

    $rootIndexPath = Join-Path $Destination "$Combine$audioTag.html"
    Set-Content $rootIndexPath $html.OuterHtml

    $epubPath = Join-Path $Destination "$Combine$audioTag.epub"
    ebook-convert $rootIndexPath $epubPath --authors "substudy" --language $Language
}