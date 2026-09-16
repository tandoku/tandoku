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
            if ($reference -match '^(?:[a-z][a-z0-9+.-]*:|//)') {
                continue
            }

            $relativePath = [Net.WebUtility]::HtmlDecode($reference)
            $relativePath = [Uri]::UnescapeDataString(($relativePath -split '[?#]', 2)[0])
            $mediaPath = Join-Path $VolumePath $relativePath
            Get-Item -LiteralPath $mediaPath -ErrorAction Stop
        }
    }
}

Export-ModuleMember -Function GetReferencedMedia
