param(
    [Parameter()]
    [String[]]
    $InputPath,

    [Parameter()]
    [String]
    $OutputPath,

    [Parameter()]
    [String]
    $TitleSuffix,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function ApplyEpubFixes($epubPath, $tempDestination) {
    ExpandArchive -Path $epubPath -DestinationPath $tempDestination -ClobberDestination

    # Disabling this for now since it's really only an issue for the first 9 footnotes
    # Could also use a shorter prefix like '#'
    #PrefixFootnotes $tempDestination 'ref'

    RenameAudioMpegaToMp3 $tempDestination

    Push-Location $tempDestination
    CompressArchive -Path * -DestinationPath $epubPath -Force
    Pop-Location
}

function PrefixFootnotes($epubContentPath, $prefix) {
    # pandoc always writes footnotes as increasing integers which can make for a small target on touchscreen devices
    # so rewrite the footnotes to include the reference name as a prefix
    Get-ChildItem "$epubContentPath/EPUB/text" -Filter "ch*.xhtml" |
        ReplaceStringInFiles 'role="doc-(noteref|backlink)">(\d+)</a>' ('role="doc-$1">' + $prefix + '-$2</a>')
}

function RenameAudioMpegaToMp3($epubContentPath) {
    # pandoc renames audio files to mpega extension which breaks KyBook 3 on iOS
    # rename mpega back to mp3
    $mediaPath = "$epubContentPath/EPUB/media"
    if (Test-Path $mediaPath) {
        Get-ChildItem $mediaPath -Filter '*.mpega' | ForEach-Object {
            $baseName = Split-Path $_ -LeafBase
            Move-Item -LiteralPath $_ "$mediaPath/$baseName.mp3"
        }
        Get-ChildItem "$epubContentPath/EPUB/text" -Filter "ch*.xhtml" |
            ReplaceStringInFiles '(src|href)="([^"]+)\.mpega"' '$1="$2.mp3"'
    }
}

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

if (-not $InputPath) {
    $InputPath = "$volumePath/markdown"
}

$markdownFiles = Get-ChildItem $InputPath -Filter *.md
if (-not $markdownFiles) {
    Write-Warning "No markdown files found in $InputPath, nothing to do"
    return
}

if ($OutputPath) {
    # $OutputPath may need to be resolved to a concrete path (e.g. if using ~)
    # TODO - add util function to do this
    $targetDirectory = Split-Path $OutputPath -Parent
    $targetFileName = Split-Path $OutputPath -Leaf
    CreateDirectoryIfNotExists $targetDirectory
    $targetDirectory = Resolve-Path $targetDirectory
    $targetPath = Join-Path $targetDirectory $targetFileName
} else {
    $targetDirectory = "$volumePath/export"
    CreateDirectoryIfNotExists $targetDirectory

    # TODO - add this as another property on volume info
    # also consider dropping the moniker from this (just the cleaned title)
    $volumeBaseFileName = Split-Path $volumePath -Leaf
    $targetPath = Join-Path $targetDirectory "$volumeBaseFileName.epub"
}

# pandoc resolves references to resources (e.g. images, audio) based on the current working directory,
# not the directory of the input files
Push-Location $volumePath
# Note that we do not use --file-scope because it breaks html splitting in the epub output (https://github.com/jgm/pandoc/issues/8741)
# TandokuMarkdownExport writes out unique footnotes across files so --file-scope is not needed
pandoc $markdownFiles -f commonmark+footnotes -o $targetPath -t epub3 --metadata title="$($volume.definition.title)$TitleSuffix" --metadata author="tandoku" --metadata lang=$($volume.definition.language)
Pop-Location

$tempDestination = "$volumePath/temp/epub"
ApplyEpubFixes $targetPath $tempDestination

Write-Output (Get-Item $targetPath)
