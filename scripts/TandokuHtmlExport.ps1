param(
    [Parameter()]
    [ValidateSet('Slides', 'Book')]
    [String]
    $Format = 'Slides',

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

$targetDirectory = "$volumePath/export"
CreateDirectoryIfNotExists $targetDirectory

$markdownFiles = Get-ChildItem "$volumePath/markdown/ref-blurhtml" -Filter *.md

# TODO - add this as another property on volume info
# also consider dropping the moniker from this (just the cleaned title)
$volumeBaseFileName = Split-Path $volumePath -Leaf
$targetPath = Join-Path $targetDirectory "$volumeBaseFileName.html.zip"

$tempDestination = "$volumePath/temp/html"
CreateDirectoryIfNotExists $tempDestination

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
            $htmlFilePath = Join-Path $tempDestination "$fileNameBase.html"
            $sectionTitle = GetContentBaseName $_
            pandoc $_ -f commonmark -o $htmlFilePath -t slidy --standalone --css ./styles/blurtext.css --variable=slidy-url:. --metadata title="$($volume.definition.title) - $sectionTitle" --metadata author="tandoku" --metadata lang=ja
            return [PSCustomObject]@{
                SectionTitle = $sectionTitle
                FileName = Split-Path $htmlFilePath -Leaf
                Path = $htmlFilePath
            }
        }
    
    # Create index html
    $listItemsHtml = $htmlFiles |
        ForEach-Object {
            return "<li><a href='$([Web.HttpUtility]::HtmlAttributeEncode($_.FileName))'>$([Web.HttpUtility]::HtmlEncode($_.SectionTitle))</a></li>"
        } | Join-String -Separator ([Environment]::NewLine)

    $indexHtml = @"
<html xmlns="http://www.w3.org/1999/xhtml">
    <head>
        <title>$($volume.definition.title)</title>
    </head>
    <body>
        <h1>$($volume.definition.title)</h1>
        <p>
$listItemsHtml
        </p>
    </body>
</html>
"@
    $indexHtmlPath = Join-Path $tempDestination 'index.html'
    Set-Content $indexHtmlPath $indexHtml

    # Copy additional resources
    CreateDirectoryIfNotExists "$tempDestination/scripts"
    Copy-Item "$PSScriptRoot/../resources/scripts/slidy.js" "$tempDestination/scripts"

    CreateDirectoryIfNotExists "$tempDestination/styles"
    Copy-Item "$PSScriptRoot/../resources/styles/slidy.css" "$tempDestination/styles"
    Copy-Item "$PSScriptRoot/../resources/styles/blurtext.css" "$tempDestination/styles"
}

CreateDirectoryIfNotExists "$tempDestination/images"
$imageExtensions = @('.jpg','.jpeg')
foreach ($imageExtension in $imageExtensions) {
    Copy-Item -Path "$volumePath/images/*$imageExtension" -Destination "$tempDestination/images/"
}

CompressArchive -Path "$tempDestination/*" -DestinationPath $targetPath -Force