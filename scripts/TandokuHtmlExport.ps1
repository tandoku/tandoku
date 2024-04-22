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

if ($OutputPath) {
    $targetDirectory = Split-Path $OutputPath -Parent
    $targetPath = $OutputPath
} else {
    $targetDirectory = "$volumePath/export"

    # TODO - add this as another property on volume info
    # also consider dropping the moniker from this (just the cleaned title)
    $volumeBaseFileName = Split-Path $volumePath -Leaf
    $targetPath = Join-Path $targetDirectory "$volumeBaseFileName.html.zip"
}
CreateDirectoryIfNotExists $targetDirectory

$tempDestination = "$volumePath/temp/html"
CreateDirectoryIfNotExists $tempDestination -Clobber

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
            $sectionTitle = GetContentBaseName $_ # TODO: read this from the content itself rather than using the filename
            pandoc $_ -f commonmark -o $htmlFilePath -t slidy --standalone --css ./styles/blurtext.css --variable=slidy-url:. --metadata title="$($volume.definition.title) - $sectionTitle" --metadata author="tandoku" --metadata lang=ja
            return [PSCustomObject]@{
                SectionTitle = $sectionTitle
                FileName = Split-Path $htmlFilePath -Leaf
                Path = $htmlFilePath
            }
        }
    
    # Create index html via markdown/pandoc
    $indexHtmlPath = Join-Path $tempDestination 'index.html'
    $htmlFiles |
        ForEach-Object {
            "- [$($_.SectionTitle)]($($_.FileName))"
        } |
        Join-String -Separator ([Environment]::NewLine) |
        pandoc -f commonmark -o $indexHtmlPath -t html --standalone --metadata title="$($volume.definition.title)" --metadata author="tandoku" --metadata lang=ja

    # Copy additional resources
    CreateDirectoryIfNotExists "$tempDestination/scripts"
    Copy-Item "$PSScriptRoot/../resources/scripts/slidy.js" "$tempDestination/scripts"

    CreateDirectoryIfNotExists "$tempDestination/styles"
    Copy-Item "$PSScriptRoot/../resources/styles/slidy.css" "$tempDestination/styles"
    # We could include blurtext.css only if input markdown needs it but doesn't seem worth having another option for this
    Copy-Item "$PSScriptRoot/../resources/styles/blurtext.css" "$tempDestination/styles"
}

CreateDirectoryIfNotExists "$tempDestination/images"
$imageExtensions = @('.jpg','.jpeg') # TODO: share this across scripts
foreach ($imageExtension in $imageExtensions) {
    Copy-Item -Path "$volumePath/images/*$imageExtension" -Destination "$tempDestination/images/"
}

CompressArchive -Path "$tempDestination/*" -DestinationPath $targetPath -Force