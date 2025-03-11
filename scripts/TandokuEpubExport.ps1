param(
    [Parameter()]
    [String[]]
    $InputPath,

    [Parameter()]
    [String]
    $OutputPath,

    [Parameter()]
    [Switch]
    $Combine,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function GenerateEpub($markdownFiles, [string]$targetPath, [string]$title) {
    # pandoc resolves references to resources (e.g. images, audio) based on the current working directory,
    # not the directory of the input files
    Push-Location $volumePath
    # Note that we do not use --file-scope because it breaks html splitting in the epub output (https://github.com/jgm/pandoc/issues/8741)
    # TandokuMarkdownExport writes out unique footnotes across files so --file-scope is not needed
    pandoc $markdownFiles -f commonmark+footnotes -o $targetPath -t epub3 --metadata title="$title" --metadata author="tandoku" --metadata lang="$($volume.definition.language)"
    Pop-Location

    $tempDestination = "$volumePath/temp/epub"
    ApplyEpubFixes $targetPath $tempDestination

    return (Get-Item $targetPath)
}

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

# TODO - move this into utility when needed elsewhere
function ExtractUniqueNamePart($files) {
    $filenames = $files | Select-Object -ExpandProperty Name
    $commonPrefix = GetCommonPrefix $filenames
    $commonSuffix = GetCommonSuffix $filenames
    $files | ForEach-Object {
        $unique = $_.Name.Substring(
            $commonPrefix.Length,
            $_.Name.Length - $commonPrefix.Length - $commonSuffix.Length)
        return [PSCustomObject]@{
            File = $_
            UniquePart = $unique
        }
    }
}

function GetCommonPrefix($list) {
    if (-not $list) {
        return $null
    }

    $prefix = $list[0]
    foreach ($item in $list) {
        while (-not $item.StartsWith($prefix) -and $prefix.Length -gt 0) {
            $prefix = $prefix.Substring(0, $prefix.Length - 1)
        }
    }
    return $prefix
}

function GetCommonSuffix($list) {
    if (-not $list) {
        return $null
    }

    $suffix = $list[0]
    foreach ($item in $list) {
        while (-not $item.EndsWith($suffix) -and $suffix.Length -gt 0) {
            $suffix = $suffix.Substring(1)
        }
    }
    return $suffix
}

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path
$volumeSlug = $volume.slug

if (-not $InputPath) {
    $InputPath = "$volumePath/markdown"
}

$markdownFiles = Get-ChildItem $InputPath -Filter *.md
if (-not $markdownFiles) {
    Write-Warning "No markdown files found in $InputPath, nothing to do"
    return
} elseif ($markdownFiles.Count -eq 1) {
    $Combine = $true
}

if ($OutputPath) {
    $targetPath = ConvertPath $OutputPath
} else {
    $targetPath = "$volumePath/export"
    CreateDirectoryIfNotExists $targetPath
}

$title = "$($volume.definition.title ?? $volumeSlug)"
if ($Combine) {
    if ((Split-Path $targetPath -Extension) -ne '.epub') {
        $targetPath = Join-Path $targetPath "$volumeSlug.epub"
    }

    $epub = GenerateEpub $markdownFiles $targetPath $title
    Write-Output $epub
} else {
    $files = ExtractUniqueNamePart $markdownFiles
    foreach ($file in $files) {
        $markdownFile = $file.File
        $fileSuffix = $file.UniquePart

        $epub = GenerateEpub $markdownFile "$targetPath/$volumeSlug-$fileSuffix.epub" "$title-$fileSuffix"
        Write-Output $epub
    }
}
