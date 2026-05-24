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
    [int]
    $SplitLevel = 2,

    [Parameter()]
    [ValidateSet('None', 'KyBook3')]
    [String]
    $Quirks = 'None',

    # Keep per-chapter footnote sections inline in each chapter instead of moving them into
    # a consolidated footnotes.xhtml file at the end of the EPUB.
    [Parameter()]
    [Switch]
    $InlineFootnotes,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function GenerateEpub($markdownFiles, [string]$targetPath, [string]$title) {
    $tempPath = "$volumePath/temp"
    CreateDirectoryIfNotExists $tempPath

    $tempEpub = "$tempPath/temp.epub"
    $tempDestination = "$tempPath/epub"

    # pandoc resolves references to resources (e.g. images, audio) based on the current working directory,
    # not the directory of the input files
    Push-Location $volumePath
    # Note that we do not use --file-scope because it breaks html splitting in the epub output (https://github.com/jgm/pandoc/issues/8741)
    # TandokuMarkdownExport writes out unique footnotes across files so --file-scope is not needed
    pandoc $markdownFiles -f commonmark+fenced_divs+footnotes -o $tempEpub -t epub3 `
        --metadata title="$title" `
        --metadata author="tandoku" `
        --metadata lang="$($volume.definition.language)" `
        --split-level=$SplitLevel

        # this suppresses the default stylesheet for epub
        #--css "$PSScriptRoot/../resources/styles/epub.css" `

    Pop-Location

    # Footnote separation is incompatible with KyBook3 (it relies on inline footnote rendering)
    $separateFootnotes = (-not $InlineFootnotes) -and ($Quirks -ne 'KyBook3')

    if ($Quirks -eq 'KyBook3' -or $separateFootnotes) {
        ApplyEpubFixes $tempEpub $tempDestination $separateFootnotes
    }

    # Move epub to target path only after applying fixes
    # so any cloud upload does not start on pre-fixed epub
    if (Test-Path $targetPath) {
        Remove-Item $targetPath
    }
    
    # TODO - currently output of pandoc and ApplyEpubFixes is also returned
    # so caller cannot do anything useful with this output
    Move-Item $tempEpub $targetPath -PassThru
}

function ApplyEpubFixes($epubPath, $tempDestination, [bool]$separateFootnotes) {
    ExpandArchive -Path $epubPath -DestinationPath $tempDestination -ClobberDestination

    # Disabling this for now since it's really only an issue for the first 9 footnotes
    # Could also use a shorter prefix like '#'
    #PrefixFootnotes $tempDestination 'ref'

    if ($separateFootnotes) {
        MoveFootnotesToSeparateFile $tempDestination
    }

    if ($Quirks -eq 'KyBook3') {
        RenameAudioMpegaToMp3 $tempDestination
    }

    CompressEpub $tempDestination $epubPath
}

function CompressEpub([string]$sourceDirectory, [string]$epubPath) {
    # The EPUB OCF spec (and epubcheck PKG-006) requires the 'mimetype' file to be the first
    # entry in the zip archive, stored uncompressed and with no extra fields. Neither
    # Compress-Archive nor `7z a -tzip` honors this, so build the zip directly with
    # System.IO.Compression so we can control entry order and compression per entry.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $sourceDirectory = (Resolve-Path -LiteralPath $sourceDirectory).Path
    $epubPath = ConvertPath $epubPath

    if (Test-Path -LiteralPath $epubPath) {
        Remove-Item -LiteralPath $epubPath
    }

    $stream = [IO.File]::Open($epubPath, [IO.FileMode]::CreateNew)
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
        try {
            # mimetype must be the first entry and stored uncompressed
            $mimetypePath = Join-Path $sourceDirectory 'mimetype'
            if (-not (Test-Path -LiteralPath $mimetypePath)) {
                throw "mimetype file not found at $mimetypePath"
            }
            $mimetypeEntry = $zip.CreateEntry('mimetype', [IO.Compression.CompressionLevel]::NoCompression)
            $entryStream = $mimetypeEntry.Open()
            try {
                $bytes = [IO.File]::ReadAllBytes($mimetypePath)
                $entryStream.Write($bytes, 0, $bytes.Length)
            } finally {
                $entryStream.Dispose()
            }

            # Add all other files preserving directory structure with forward-slash entry names
            Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File |
                Where-Object { $_.FullName -ne $mimetypePath } |
                ForEach-Object {
                    [pscustomobject]@{
                        FullName = $_.FullName
                        RelativePath = [IO.Path]::GetRelativePath($sourceDirectory, $_.FullName).Replace('\', '/')
                    }
                } |
                Sort-Object RelativePath |
                ForEach-Object {
                    [void] [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                        $zip, $_.FullName, $_.RelativePath, [IO.Compression.CompressionLevel]::Optimal)
                }
        } finally {
            $zip.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

function MoveFootnotesToSeparateFile($epubContentPath) {
    # pandoc emits footnotes inline as a <section epub:type="footnotes"> at the end of each
    # chapter xhtml. Some EPUB readers render those inline at the end of the chapter even
    # when pop-up footnotes are supported. Extract the footnote <aside>s from each chapter,
    # rewrite the noteref/backlink anchors so they cross files, and gather everything into a
    # single text/footnotes.xhtml registered at the end of the spine.
    $textPath = "$epubContentPath/EPUB/text"
    $chapterFiles = @(Get-ChildItem $textPath -Filter "ch*.xhtml" | Sort-Object Name)

    $allAsides = [Text.StringBuilder]::new()

    foreach ($file in $chapterFiles) {
        $content = Get-Content -Raw -LiteralPath $file.FullName
        $sectionMatch = [regex]::Match($content,
            '(?s)\s*<section\b[^>]*\bepub:type="footnotes"[^>]*>.*?</section>')
        if (-not $sectionMatch.Success) { continue }

        $basename = [IO.Path]::GetFileNameWithoutExtension($file.Name)
        $sectionHtml = $sectionMatch.Value

        # Remove the inline footnotes section and rewrite the noteref anchors that remain
        # in the body so they point at the new footnotes file with chapter-prefixed ids.
        $remaining = $content.Remove($sectionMatch.Index, $sectionMatch.Length)
        $remaining = [regex]::Replace($remaining, 'href="#(fn\d+)"',
            "href=`"footnotes.xhtml#$basename-`$1`"")
        $remaining = [regex]::Replace($remaining, 'id="(fnref\d+)"',
            "id=`"$basename-`$1`"")
        Set-Content -LiteralPath $file.FullName -Value $remaining -NoNewline

        # Rewrite the aside ids and backlink hrefs to match the relocated anchors, then
        # collect the aside elements for the consolidated footnotes file.
        $sectionHtml = [regex]::Replace($sectionHtml, 'id="(fn\d+)"',
            "id=`"$basename-`$1`"")
        $sectionHtml = [regex]::Replace($sectionHtml, 'href="#(fnref\d+)"',
            "href=`"$basename.xhtml#$basename-`$1`"")

        $asideMatches = [regex]::Matches($sectionHtml,
            '(?s)<aside\b[^>]*\bepub:type="footnote"[^>]*>.*?</aside>')
        foreach ($am in $asideMatches) {
            [void] $allAsides.AppendLine($am.Value)
        }
    }

    if ($allAsides.Length -eq 0) { return }

    $lang = $volume.definition.language
    $langAttrs = $lang ? " lang=`"$lang`" xml:lang=`"$lang`"" : ''

    $footnotesXhtml = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"$langAttrs>
<head>
  <meta charset="utf-8" />
  <meta name="generator" content="tandoku" />
  <title>Footnotes</title>
  <link rel="stylesheet" type="text/css" href="../styles/stylesheet1.css" />
</head>
<body epub:type="backmatter">
<section epub:type="footnotes" role="doc-endnotes" class="footnotes">
<h1>Footnotes</h1>
$($allAsides.ToString().TrimEnd())
</section>
</body>
</html>
"@
    Set-Content -LiteralPath "$textPath/footnotes.xhtml" -Value $footnotesXhtml -NoNewline

    # Register the new file in the OPF manifest and spine
    $opfPath = "$epubContentPath/EPUB/content.opf"
    $opf = Get-Content -Raw -LiteralPath $opfPath
    $opf = $opf -replace '(\r?\n)(\s*)</manifest>',
        "`$1`$2  <item id=`"footnotes_xhtml`" href=`"text/footnotes.xhtml`" media-type=`"application/xhtml+xml`" />`$1`$2</manifest>"
    $opf = $opf -replace '(\r?\n)(\s*)</spine>',
        "`$1`$2  <itemref idref=`"footnotes_xhtml`" linear=`"no`" />`$1`$2</spine>"
    Set-Content -LiteralPath $opfPath -Value $opf -NoNewline
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
    # TODO - this isn't updating the manifest so the resulting EPUB isn't fully valid
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

    GenerateEpub $markdownFiles $targetPath $title
} else {
    $files = ExtractUniqueNamePart $markdownFiles
    foreach ($file in $files) {
        $markdownFile = $file.File
        $fileSuffix = $file.UniquePart

        GenerateEpub $markdownFile "$targetPath/$volumeSlug-$fileSuffix.epub" "$title-$fileSuffix"
    }
}
