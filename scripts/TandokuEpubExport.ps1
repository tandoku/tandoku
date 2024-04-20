param(
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

$markdownFiles = Get-ChildItem "$volumePath/markdown/ref-footnotes" -Filter *.md

# TODO - add this as another property on volume info
# also consider dropping the moniker from this (just the cleaned title)
$volumeBaseFileName = Split-Path $volumePath -Leaf
$targetPath = Join-Path $targetDirectory "$volumeBaseFileName.epub"

# pandoc resolves references to resources (e.g. images, audio) based on
# the current working directory, not the directory of the input files
Push-Location $VolumePath
# TODO --file-scope causes epub output to have single xhtml file, need to renumber footnotes to avoid using --file-scope
pandoc $markdownFiles -f commonmark+footnotes --file-scope -o $targetPath -t epub3 --metadata title="$($volume.definition.title)" --metadata author="tandoku" --metadata lang=ja
Pop-Location

# pandoc always writes footnotes as increasing integers which can make for a small target on touchscreen devices
# so rewrite the footnotes to include the reference name as a prefix
$refName = 'en'

$tempDestination = "$volumePath/temp/epub"
if (Test-Path $tempDestination) {
    Remove-Item $tempDestination -Recurse -Force
}
Expand-Archive -Path $targetPath -DestinationPath $tempDestination
Get-ChildItem "$tempDestination/EPUB/text" -Filter "ch*.xhtml" |
    ReplaceStringInFiles 'role="doc-(noteref|backlink)">(\d+)</a>' ('role="doc-$1">' + $refName + '-$2</a>')
Push-Location $tempDestination
Compress-Archive -Path * -DestinationPath $targetPath -Force
Pop-Location