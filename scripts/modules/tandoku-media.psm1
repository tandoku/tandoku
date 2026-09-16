function ExtractFilmQualifierFromFileName($fileName) {
    return ($fileName -match 's\d{1,4}e\d{1,4}' ? "-$($Matches[0])".ToLowerInvariant() : $null)
}

function GetImageExtensions {
    return @('.jpg','.jpeg','.png')
}

function GetKnownAudioExtensions([Switch]$FileMask) {
    $prefix = $FileMask ? '*' : ''
    return @("$prefix.mp3","$prefix.m4a")
}

function GetKnownVideoExtensions([Switch]$FileMask) {
    $prefix = $FileMask ? '*' : ''
    return "$prefix.mkv","$prefix.mp4"
}

function GetKnownSubtitleExtensions([Switch]$FileMask, [String]$Language, [Switch]$MatchLanguagePrefix, [Switch]$TtmlOnly) {
    $prefix = $FileMask ? '*' : ''
    if ($Language) {
        $prefix = "$prefix.$Language"
        if ($MatchLanguagePrefix) {
            $prefix = "$prefix*"
        }
    }
    $result = "$prefix.ttml","$prefix.dfxp","$prefix.xml"
    if (-not $TtmlOnly) {
        $result += "$prefix.vtt","$prefix.ass","$prefix.srt"
    }
    return $result
}

function GetSubtitleBaseName($subtitle) {
    # Remove both subtitle and language extension (e.g. .ja.srt)
    return (Split-Path (Split-Path $subtitle -LeafBase) -LeafBase)
}

function GetAudioReferences([String]$html) {
    foreach ($match in [regex]::Matches(
        $html,
        '(?i)<audio\b[^>]*\bsrc\s*=\s*(?:"([^"]+)"|''([^'']+)''|([^\s>]+))')) {
        $match.Groups[1..3] |
            Where-Object Success |
            Select-Object -First 1 -ExpandProperty Value
    }
}

function GetReferencedMedia {
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [IO.FileInfo]
        $MarkdownFile,

        [Parameter(Mandatory)]
        [String]
        $VolumePath
    )

    process {
        $markdown = ConvertFrom-Markdown -LiteralPath $MarkdownFile.FullName
        $references = [Collections.Generic.HashSet[string]]::new(
            [StringComparer]::OrdinalIgnoreCase)
        $pending = [Collections.Queue]::new()
        $pending.Enqueue($markdown.Tokens)

        while ($pending.Count -gt 0) {
            $node = $pending.Dequeue()
            $typeName = $node.GetType().FullName

            if (($typeName -eq 'Markdig.Syntax.Inlines.LinkInline') -and $node.IsImage) {
                [void] $references.Add($node.Url)
            } elseif ($typeName -eq 'Markdig.Syntax.Inlines.HtmlInline') {
                foreach ($reference in GetAudioReferences $node.Tag) {
                    [void] $references.Add($reference)
                }
            } elseif ($typeName -eq 'Markdig.Syntax.HtmlBlock') {
                foreach ($reference in GetAudioReferences $node.Lines.ToString()) {
                    [void] $references.Add($reference)
                }
            }

            if ($node.PSObject.Properties['Inline'] -and $node.Inline) {
                $pending.Enqueue($node.Inline)
            }
            if ($node -is [Collections.IEnumerable]) {
                foreach ($child in $node) {
                    $pending.Enqueue($child)
                }
            }
        }

        foreach ($reference in $references) {
            # Remote references do not resolve to local media files.
            if ($reference -match '^(?:[a-z][a-z0-9+.-]*:|//)') {
                continue
            }

            # Decode HTML entities used in attribute values.
            $relativePath = [Net.WebUtility]::HtmlDecode($reference)
            # Remove URL components that are not part of the filesystem path.
            $relativePath = ($relativePath -split '[?#]', 2)[0]
            # Decode percent-encoded path characters.
            $relativePath = [Uri]::UnescapeDataString($relativePath)
            $mediaPath = Join-Path $VolumePath $relativePath
            Get-Item -LiteralPath $mediaPath -ErrorAction Stop
        }
    }
}

Export-ModuleMember -Function `
    ExtractFilmQualifierFromFileName, `
    GetImageExtensions, `
    GetKnownAudioExtensions, `
    GetKnownVideoExtensions, `
    GetKnownSubtitleExtensions, `
    GetSubtitleBaseName, `
    GetReferencedMedia
