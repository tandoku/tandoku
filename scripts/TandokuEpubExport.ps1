param(
    [Parameter()]
    [String]
    $InputPath,

    [Parameter()]
    [String]
    $OutputPath,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function PrefixFootnotes($epubPath, $tempDestination, $prefix) {
    ExpandArchive -Path $epubPath -DestinationPath $tempDestination -ClobberDestination
    Get-ChildItem "$tempDestination/EPUB/text" -Filter "ch*.xhtml" |
        ReplaceStringInFiles 'role="doc-(noteref|backlink)">(\d+)</a>' ('role="doc-$1">' + $prefix + '-$2</a>')
    Push-Location $tempDestination
    CompressArchive -Path * -DestinationPath $epubPath -Force
    Pop-Location
}

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$markdownDirectory = $InputPath ? $InputPath : "$volumePath/markdown"
$markdownFiles = Get-ChildItem $markdownDirectory -Filter *.md
if (-not $markdownFiles) {
    Write-Warning "No markdown files found in $markdownDirectory, nothing to do"
    return
}

if ($OutputPath) {
    $targetDirectory = Split-Path $OutputPath -Parent
    $targetPath = $OutputPath
} else {
    $targetDirectory = "$volumePath/export"

    # TODO - add this as another property on volume info
    # also consider dropping the moniker from this (just the cleaned title)
    $volumeBaseFileName = Split-Path $volumePath -Leaf
    $targetPath = Join-Path $targetDirectory "$volumeBaseFileName.epub"
}
CreateDirectoryIfNotExists $targetDirectory

# pandoc resolves references to resources (e.g. images, audio) based on the current working directory,
# not the directory of the input files
Push-Location $volumePath
# Note that we do not use --file-scope because it breaks html splitting in the epub output (https://github.com/jgm/pandoc/issues/8741)
# TandokuMarkdownExport writes out unique footnotes across files so --file-scope is not needed
pandoc $markdownFiles -f commonmark+footnotes -o $targetPath -t epub3 --metadata title="$($volume.definition.title)" --metadata author="tandoku" --metadata lang=ja
Pop-Location

# pandoc always writes footnotes as increasing integers which can make for a small target on touchscreen devices
# so rewrite the footnotes to include the reference name as a prefix
$refName = 'en'
$tempDestination = "$volumePath/temp/epub"
PrefixFootnotes $targetPath $tempDestination $refName

Write-Output (Get-Item $targetPath)
