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

    # Hide the section headings in the chapter body while keeping them available for
    # TOC navigation. This also removes the leading whitespace they introduce.
    [Parameter()]
    [Switch]
    $HideSectionHeadings,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function GenerateEpub($markdownFiles, [string]$targetPath, [string]$title) {
    $tempPath = "$volumePath/temp"
    CreateDirectoryIfNotExists $tempPath

    $tempEpub = "$tempPath/temp.epub"

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

    ApplyEpubFixes $tempEpub $separateFootnotes

    # Move epub to target path only after applying fixes
    # so any cloud upload does not start on pre-fixed epub
    if (Test-Path $targetPath) {
        Remove-Item $targetPath
    }
    
    # TODO - currently output of pandoc and ApplyEpubFixes is also returned
    # so caller cannot do anything useful with this output
    Move-Item $tempEpub $targetPath -PassThru
}

function ApplyEpubFixes([string]$epubPath, [bool]$separateFootnotes) {
    # All fixes are applied by editing the text/style entries of the pandoc-generated EPUB
    # zip in place. The media entries (images, audio) are left untouched, so they are neither
    # re-read from disk nor re-compressed - that media handling was the bulk of the work for
    # media-heavy volumes when the EPUB was fully expanded and recompressed. Editing in Update
    # mode preserves the pandoc entry order, so the 'mimetype' entry stays first and stored
    # uncompressed as the EPUB OCF spec (and epubcheck PKG-006) requires.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $resolvedPath = ConvertPath $epubPath
    $zip = [IO.Compression.ZipFile]::Open($resolvedPath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        PrefixFootnotes $zip '#'

        FixHorizontalRules $zip

        if ($HideSectionHeadings) {
            HideSectionHeadings $zip
        }

        if ($separateFootnotes) {
            MoveFootnotesToSeparateFile $zip
        }

        if ($Quirks -eq 'KyBook3') {
            RenameAudioMpegaToMp3 $zip
        }
    } finally {
        $zip.Dispose()
    }
}

# UTF-8 without a byte order mark, matching pandoc's EPUB output so rewritten entries stay
# byte-for-byte compatible with the rest of the archive.
$script:EpubEntryEncoding = [Text.UTF8Encoding]::new($false)

# Entry name of the pandoc-generated EPUB stylesheet that the fixes read from and write back to.
$script:StylesheetEntry = 'EPUB/styles/stylesheet1.css'

function ReadEpubEntry([IO.Compression.ZipArchive]$zip, [string]$entryName) {
    $entry = $zip.GetEntry($entryName)
    if (-not $entry) {
        return $null
    }
    $stream = $entry.Open()
    try {
        $reader = [IO.StreamReader]::new($stream)
        return $reader.ReadToEnd()
    } finally {
        $stream.Dispose()
    }
}

function WriteEpubEntry([IO.Compression.ZipArchive]$zip, [string]$entryName, [string]$content) {
    $entry = $zip.GetEntry($entryName)
    if (-not $entry) {
        $entry = $zip.CreateEntry($entryName)
    }
    $stream = $entry.Open()
    try {
        $stream.SetLength(0)
        $writer = [IO.StreamWriter]::new($stream, $script:EpubEntryEncoding)
        $writer.Write($content)
        $writer.Flush()
    } finally {
        $stream.Dispose()
    }
}

function GetChapterEntries([IO.Compression.ZipArchive]$zip) {
    return @($zip.Entries | Where-Object { $_.FullName -like 'EPUB/text/ch*.xhtml' })
}

function FixHorizontalRules([IO.Compression.ZipArchive]$zip) {
    # pandoc's default EPUB stylesheet draws <hr/> using only background-color with
    # `border: none`. Many readers (B&N Nook, Readium-based apps like Thorium) drop
    # publisher background-color so they can apply their own themes, which makes the
    # rule invisible. Redraw the rule with a border so it renders reliably.
    $css = ReadEpubEntry $zip $script:StylesheetEntry
    if ($null -eq $css) {
        return
    }

    $fixedHr = @"
hr {
  border: none;
  border-top: 1px solid #1a1a1a;
  height: 0;
  margin: 1em 0;
}
"@
    $fixedCss = [regex]::Replace($css, 'hr\s*\{[^}]*\}', $fixedHr)
    if ($fixedCss -ne $css) {
        WriteEpubEntry $zip $script:StylesheetEntry $fixedCss
    }
}

function HideSectionHeadings([IO.Compression.ZipArchive]$zip) {
    # pandoc emits each section heading at the top of the chapter and styles it
    # with a large top margin, producing a tall band of whitespace before the content. These
    # headings exist mainly to drive TOC/navigation, but the nav documents target the enclosing
    # <section> ids (e.g. #section, #section-1) rather than the heading elements, so the headings
    # can be hidden without breaking navigation. Using display:none also collapses the heading
    # box (and its margin), which removes the leading whitespace. display:none is broadly
    # supported by EPUB readers (B&N Nook, Readium/Thorium, etc.). Only the heading level that
    # starts each split chapter (-SplitLevel) is targeted; the title page and footnotes headings
    # stay visible.
    $css = ReadEpubEntry $zip $script:StylesheetEntry
    if ($null -eq $css) {
        return
    }

    $hideHeadings = @"

section[class~="level$SplitLevel"] > h$SplitLevel {
  display: none;
}
"@
    WriteEpubEntry $zip $script:StylesheetEntry ($css.TrimEnd() + "`n" + $hideHeadings)
}

function MoveFootnotesToSeparateFile([IO.Compression.ZipArchive]$zip) {
    # pandoc emits footnotes inline as a <section epub:type="footnotes"> at the end of each
    # chapter xhtml. Some EPUB readers render those inline at the end of the chapter even
    # when pop-up footnotes are supported. Extract the footnote <aside>s from each chapter,
    # rewrite the noteref/backlink anchors so they cross files, and gather everything into a
    # single text/footnotes.xhtml registered at the end of the spine.
    $chapterEntries = @(GetChapterEntries $zip | Sort-Object FullName)

    $allAsides = [Text.StringBuilder]::new()

    foreach ($entry in $chapterEntries) {
        $content = ReadEpubEntry $zip $entry.FullName
        $sectionMatch = [regex]::Match($content,
            '(?s)\s*<section\b[^>]*\bepub:type="footnotes"[^>]*>.*?</section>')
        if (-not $sectionMatch.Success) { continue }

        $basename = [IO.Path]::GetFileNameWithoutExtension($entry.Name)
        $sectionHtml = $sectionMatch.Value

        # Remove the inline footnotes section and rewrite the noteref anchors that remain
        # in the body so they point at the new footnotes file with chapter-prefixed ids.
        $remaining = $content.Remove($sectionMatch.Index, $sectionMatch.Length)
        $remaining = [regex]::Replace($remaining, 'href="#(fn\d+)"',
            "href=`"footnotes.xhtml#$basename-`$1`"")
        $remaining = [regex]::Replace($remaining, 'id="(fnref\d+)"',
            "id=`"$basename-`$1`"")
        WriteEpubEntry $zip $entry.FullName $remaining

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
    WriteEpubEntry $zip 'EPUB/text/footnotes.xhtml' $footnotesXhtml

    # Register the new file in the OPF manifest and spine
    $opf = ReadEpubEntry $zip 'EPUB/content.opf'
    $opf = $opf -replace '(\r?\n)(\s*)</manifest>',
        "`$1`$2  <item id=`"footnotes_xhtml`" href=`"text/footnotes.xhtml`" media-type=`"application/xhtml+xml`" />`$1`$2</manifest>"
    $opf = $opf -replace '(\r?\n)(\s*)</spine>',
        "`$1`$2  <itemref idref=`"footnotes_xhtml`" linear=`"no`" />`$1`$2</spine>"
    WriteEpubEntry $zip 'EPUB/content.opf' $opf
}

function PrefixFootnotes([IO.Compression.ZipArchive]$zip, $prefix) {
    # pandoc always writes footnotes as increasing integers which can make for a small target on touchscreen devices
    # so rewrite the footnotes to include a prefix
    $search = 'role="doc-(noteref|backlink)">(\d+)</a>'
    $replace = 'role="doc-$1">' + $prefix + '$2</a>'
    foreach ($entry in GetChapterEntries $zip) {
        $content = ReadEpubEntry $zip $entry.FullName
        $updated = [regex]::Replace($content, $search, $replace)
        if ($updated -ne $content) {
            WriteEpubEntry $zip $entry.FullName $updated
        }
    }
}

function RenameAudioMpegaToMp3([IO.Compression.ZipArchive]$zip) {
    # pandoc renames audio files to mpega extension which breaks KyBook 3 on iOS
    # rename mpega back to mp3
    # TODO - this isn't updating the manifest so the resulting EPUB isn't fully valid
    $mpegaEntries = @($zip.Entries | Where-Object { $_.FullName -like 'EPUB/media/*.mpega' })
    foreach ($entry in $mpegaEntries) {
        $newName = $entry.FullName.Substring(0, $entry.FullName.Length - '.mpega'.Length) + '.mp3'

        # Read the source bytes fully before creating the destination entry; Update mode does
        # not allow two entries to be open at the same time.
        $source = $entry.Open()
        try {
            $buffer = [IO.MemoryStream]::new()
            $source.CopyTo($buffer)
        } finally {
            $source.Dispose()
        }

        $newEntry = $zip.CreateEntry($newName, [IO.Compression.CompressionLevel]::NoCompression)
        $target = $newEntry.Open()
        try {
            $bytes = $buffer.ToArray()
            $target.Write($bytes, 0, $bytes.Length)
        } finally {
            $target.Dispose()
        }

        $entry.Delete()
    }

    foreach ($entry in GetChapterEntries $zip) {
        $content = ReadEpubEntry $zip $entry.FullName
        $updated = [regex]::Replace($content, '(src|href)="([^"]+)\.mpega"', '$1="$2.mp3"')
        if ($updated -ne $content) {
            WriteEpubEntry $zip $entry.FullName $updated
        }
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
