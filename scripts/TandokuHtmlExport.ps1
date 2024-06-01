param(
    [Parameter()]
    [String]
    $InputPath,

    [Parameter()]
    [String]
    $OutputPath,

    [Parameter()]
    [ValidateSet('Slides', 'Book')]
    [String]
    $Format = 'Slides',

    [Parameter()]
    [Switch]
    $ZipArchive,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$markdownDirectory = $InputPath ? $InputPath : "$volumePath/markdown"
$markdownFiles = Get-ChildItem $markdownDirectory -Filter *.md

if ($ZipArchive) {
    if ($OutputPath) {
        $archivePath = $OutputPath
    } else {
        # TODO - add this as another property on volume info
        # also consider dropping the moniker from this (just the cleaned title)
        $volumeBaseFileName = Split-Path $volumePath -Leaf
        $archivePath = "$volumePath/export/$volumeBaseFileName.html-$($Format.ToLowerInvariant()).zip"
    }

    $targetDirectory = "$volumePath/temp/html"
    CreateDirectoryIfNotExists $targetDirectory -Clobber
} else {
    if ($OutputPath) {
        $targetDirectory = $OutputPath
    } else {
        $targetDirectory = "$volumePath/export/html-$($Format.ToLowerInvariant())"
    }
    CreateDirectoryIfNotExists $targetDirectory
    $targetDirectory = Resolve-Path $targetDirectory # normalize path (e.g. ~/) for use with pandoc below
}

if ($Format -eq 'Book') {
    # TODO - either need to use footnotes or add blurtext.css stylesheet
    #pandoc $markdownFiles -f commonmark+footnotes -o $targetPath -t chunkedhtml --metadata title="$($volume.definition.title)" --metadata author="tandoku" --metadata lang=ja
    #ExpandArchive -Path $targetPath -DestinationPath $tempDestination -ClobberDestination
    throw "Book format not fully implemented"
} elseif ($Format -eq 'Slides') {
    # Create slidy html for each markdown file
    $htmlFiles = $markdownFiles |
        ForEach-Object {
            $fileNameBase = Split-Path $_ -LeafBase
            $htmlFilePath = Join-Path $targetDirectory "$fileNameBase.html"
            $sectionTitle = GetContentBaseName $_ # TODO: read this from the content itself rather than using the filename
            pandoc $_ -f commonmark -o $htmlFilePath -t slidy --standalone `
                --css ./styles/blurtext.css --variable=slidy-url:. `
                --metadata title="$($volume.definition.title) - $sectionTitle" `
                --metadata author="tandoku" --metadata lang=ja
            return [PSCustomObject]@{
                SectionTitle = $sectionTitle
                FileName = Split-Path $htmlFilePath -Leaf
                Path = $htmlFilePath
            }
        }
    
    # Add previous/next file metadata to content html files to enable navigation
    # Note: could use pandoc --include-in-header to do this but would have to create a temporary file
    if ($htmlFiles.Count -gt 1) {
        for ($i = 0; $i -lt $htmlFiles.Count; $i++) {
            $htmlFilePath = $htmlFiles[$i].Path
            $html = ConvertFrom-Html -Path $htmlFilePath
            $head = $html.SelectSingleNode('html/head')
            if ($i -gt 0) {
                $prevContent = [HtmlAgilityPack.HtmlNode]::CreateNode('<meta/>')
                $prevContent.SetAttributeValue('name', 'previous-file')
                $prevContent.SetAttributeValue('content', $htmlFiles[$i - 1].FileName)
                $head.AppendChild($prevContent)
            }
            if ($i -lt $htmlFiles.Count - 1) {
                $nextContent = [HtmlAgilityPack.HtmlNode]::CreateNode('<meta/>')
                $nextContent.SetAttributeValue('name', 'next-file')
                $nextContent.SetAttributeValue('content', $htmlFiles[$i + 1].FileName)
                $head.AppendChild($nextContent)
            }
            Set-Content -Path $htmlFilePath -Value $html.OuterHtml
        }
    }

    # Create index html via markdown/pandoc
    $indexHtmlPath = Join-Path $targetDirectory 'index.html'
    $htmlFiles |
        ForEach-Object {
            "- [$($_.SectionTitle)]($($_.FileName))"
        } |
        Join-String -Separator ([Environment]::NewLine) |
        pandoc -f commonmark -o $indexHtmlPath -t html --standalone `
            --metadata title="$($volume.definition.title)" `
            --metadata author="tandoku" --metadata lang=ja

    # Copy additional resources
    CreateDirectoryIfNotExists "$targetDirectory/scripts"
    Copy-Item "$PSScriptRoot/../resources/scripts/slidy.js" "$targetDirectory/scripts"

    CreateDirectoryIfNotExists "$targetDirectory/styles"
    Copy-Item "$PSScriptRoot/../resources/styles/slidy.css" "$targetDirectory/styles"
    # Could include blurtext.css only if input markdown needs it but doesn't seem worth having another option for this
    Copy-Item "$PSScriptRoot/../resources/styles/blurtext.css" "$targetDirectory/styles"
}

CreateDirectoryIfNotExists "$targetDirectory/images"
$imageExtensions = GetImageExtensions
foreach ($imageExtension in $imageExtensions) {
    Copy-Item -Path "$volumePath/images/*$imageExtension" -Destination "$targetDirectory/images/"
}

if ($ZipArchive) {
    CompressArchive -Path "$targetDirectory/*" -DestinationPath $archivePath -Force
    Write-Output (Get-Item $archivePath)
} else {
    Write-Output (Get-Item $targetDirectory)
}