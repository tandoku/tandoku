param(
    [Parameter()]
    [String]
    $InputPath,

    [Parameter()]
    [String]
    $OutputPath,

    [Parameter()]
    [String]
    $OutputPrefix, # TODO - consider a general-purpose build task to do this later instead (apply prefix to a set of files)

    [Parameter()]
    [Switch]
    $Combine,

    # TODO - more specific or incorporated into another option
    # this is really "do not promote images to headings" or something
    # (probably make this the default and have a switch for -HeadingPerBlock or similar)
    [Parameter()]
    [Switch]
    $NoHeadings,

    [Parameter()]
    [ValidateSet('None', 'Html', 'BlurHtml', 'Remove')]
    [String]
    $RubyBehavior = 'None',

    [Parameter()]
    [ValidateSet('None', 'Footnotes', 'BlurHtml')]
    [String]
    $ReferenceBehavior = 'None',

    [Parameter()]
    [ValidateSet('Default','Always')]
    [String]
    $ReferenceLabels = 'Default',

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function GenerateMarkdown($contentPath) {
    $content = ImportYaml $contentPath

    # Note that a unique id prefix is used anytime there are multiple input files, whether or not the output
    # will be combined into a single markdown file.
    # Required by TandokuEpubExport which treats multiple markdown files as a single concatenated file.
    $idPrefix = GetContentBaseName $contentPath
    if (-not $idPrefix) {
        # Default id prefix - blockId may be used in HTML id attributes which must start with a letter
        $idPrefix = 'block'
    }

    # TODO - clean up naming, include even when writing block headings?
    if ($NoHeadings) {
        $fileHeading = Split-Path (Split-Path $contentPath -LeafBase) -LeafBase
        Write-Output "# $fileHeading"
        Write-Output ''
    }

    $blockIndex = 0
    foreach ($block in $content) {
        $blockIndex += 1
        $blockId = "$idPrefix-$blockIndex"

        # TODO - generalize this (markdown templates)

        # Heading
        $heading = GetHeading $block
        if ($heading -and -not $NoHeadings) {
            Write-Output "# $heading"
            Write-Output ''
        }

        # Image
        GenerateMarkdownForMedia $block.image.name 'images' $heading

        # Audio
        GenerateMarkdownForMedia $block.audio.name 'audio' $heading

        if ($block.chunks) {
            $chunkIndex = 0
            foreach ($chunk in $block.chunks) {
                $chunkIndex += 1
                if ($block.chunks.Count -gt 1) {
                    $chunkId = "$blockId-$chunkIndex"
                } else {
                    $chunkId = $blockId
                }
                GenerateMarkdownForChunk $chunk $chunkId
            }
        }
    }
}

function GetHeading($block) {
    $heading = $block.source.note ?? $block.source.resource
    if ($heading) {
        return $heading
    }

    if ($block.image.name) {
        return (Split-Path $block.image.name -LeafBase)
    }
}

function GenerateMarkdownForMedia($media, $container, $caption) {
    if ($media) {
        $mediaNameEncoded = [Uri]::EscapeDataString($media).Replace('%2F', '/')
        $mediaUrl = "$container/$mediaNameEncoded"
        if ($container -eq "audio") {
            # Use explicit <audio> tag because the anchor link that pandoc embeds
            # within the <audio> tag if ![]() is used causes issues for KyBook 3 on iOS
            Write-Output "<audio src=`"$mediaUrl`"></audio>"
            Write-Output ''
        } else {
            Write-Output "![$caption]($mediaUrl)"
            Write-Output ''
        }
    }
}

function GenerateMarkdownForChunk($chunk, $chunkId) {
    # Text / Reference text

    $chunkText = ProcessRubyText $chunk.text
    if ($RubyBehavior -eq 'BlurHtml') {
        $chunkText = ConvertTextToBlurHtml $chunkText $chunkId -Ruby
    }

    $chunkRefTextBuilder = [Text.StringBuilder]::new()
    $refLabels = ($chunk.references.Count -gt 1 -or $ReferenceLabels -eq 'Always')
    foreach ($refName in $chunk.references.Keys) {
        $ref = $chunk.references[$refName]
        $chunkRefText = $ref.text
        if ($chunkRefText) {
            $chunkRefId = "$chunkId-ref-$refName"

            switch ($ReferenceBehavior) {
                'Footnotes' {
                    $chunkText = "$chunkText [^$chunkRefId]"
                    $lines = @(StringToLines $chunkRefText)
                    $lines[0] = "[^$chunkRefId]: $($lines[0])"
                    foreach ($line in $lines) {
                        if ($line -and $chunkRefTextBuilder.Length -gt 0) {
                            [void] $chunkRefTextBuilder.AppendLine("    $line")
                        } else {
                            [void] $chunkRefTextBuilder.AppendLine($line)
                        }
                    }
                    $chunkRefText = $null
                }
                'BlurHtml' {
                    $chunkRefText = ConvertTextToBlurHtml $chunkRefText $chunkRefId -Label ($refLabels ? $refName : $null)
                }
                default {
                    if ($refLabels) {
                        # TODO - indentation as separate style?
                        $lines = @(StringToLines $chunkRefText)
                        $lines[0] = "$($refName): $($lines[0])"
                        foreach ($line in $lines) {
                            [void] $chunkRefTextBuilder.AppendLine("> $line")
                        }
                        $chunkRefText = $null
                    }
                }
            }

            if ($chunkRefText) {
                [void] $chunkRefTextBuilder.AppendLine($chunkRefText)
            }
            [void] $chunkRefTextBuilder.AppendLine()
        }
    }

    Write-Output $chunkText
    Write-Output ''
    if ($chunkRefTextBuilder.Length -gt 0) {
        Write-Output $chunkRefTextBuilder
    }
}

function ProcessRubyText([String]$text) {
    if ($RubyBehavior -ne 'None') {
        $rubyMatch = '(^| )([^ \[]+)\[(.+?)\]'
        $rubyReplace = switch -Wildcard ($RubyBehavior) {
            '*Html' { '<ruby><rb>$2</rb><rt>$3</rt></ruby>' }
            'Remove' { '$2' }
        }
        return $text -replace $rubyMatch,$rubyReplace
    }
    return $text
}

function ConvertTextToBlurHtml([String]$text, [String]$id, [Switch]$Ruby, [String]$Label) {
    # references
    # - https://www.w3docs.com/snippets/css/how-to-create-a-blurry-text-in-css.html
    # - https://bernholdtech.blogspot.com/2013/04/very-simple-pure-css-collapsible-list.html

    # notes
    # - pandoc will corrupt HTML output if a markdown block contains an unclosed HTML element
    #   but does not start with an HTML element. So the whole block needs to be HTML.
    # - blur works only for simple content within <span> or <p> elements; it works for complex
    #   content (e.g. <ol>) inside a <div> but not a <span> or <p>.
    # - $element logic below tries to keep the label on the same line as the content, unless
    #   there is complex content (in which case it will be rendered separately anyway).
    # - margins for slides are incorrect for simple content in a <span> or <div> without a <p>.
    #   Complex content (<ol>) is fine in a <div> though.

    if ($Ruby -and (-not $text.Contains('<ruby>'))) {
        if ($Label) {
            return "$($Label): $text"
        }
        return $text
    }

    $html = (ConvertFrom-Markdown -InputObject $text).Html.TrimEnd()
    $isPara = ($html.StartsWith('<p>') -and $html.EndsWith('</p>'))
    $element = $isPara ? ($Label ? 'span' : 'p') : 'div'

    $blurClass = $Ruby ? 'blurRuby' : 'blurText'
    $initial = "<$element class='$blurClass'><input type='checkbox' id='$id'/><label for='$id'>"
    $final = "</label></$element>"

    if ($isPara) {
        # use single-line mode so . matches newlines as well
        $html = $html -replace '(?s)^<p>(.*)</p>$',"$initial`$1$final"
    } else {
        $html = "$initial$html$final"
    }

    if ($Label) {
        $html = "<p><span>$($Label):</span> $html</p>"
    }

    return $html
}

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$contentDirectory = $InputPath ? $InputPath : "$volumePath/content"
$contentFiles =
    @(Get-ChildItem $contentDirectory -Filter content.yaml) +
    @(Get-ChildItem $contentDirectory -Filter *.content.yaml)
if (-not $contentFiles) {
    Write-Warning "No content files found in $contentDirectory, nothing to do"
    return
}

if ($OutputPath) {
    if ($Combine -and ([IO.Path]::GetExtension($OutputPath) -eq '.md')) {
        $targetDirectory = Split-Path $OutputPath -Parent
        $targetPath = $OutputPath
    } else {
        $targetDirectory = $OutputPath
    }
} else {
    $targetDirectory = "$volumePath/markdown"
}
CreateDirectoryIfNotExists $targetDirectory

if ($Combine) {
    if (-not $targetPath) {
        # TODO - add this as another property on volume info
        # also consider dropping the moniker from this (just the cleaned title)
        # -OR- just call this content.md
        $volumeBaseFileName = Split-Path $volumePath -Leaf
        $targetPath = Join-Path $targetDirectory "$OutputPrefix$volumeBaseFileName.md"
    }

    $contentFiles |
        Foreach-Object { GenerateMarkdown $_ } |
        Set-Content $targetPath

    Write-Output (Get-Item $targetPath)
} else {
    $contentFiles |
        Foreach-Object {
            $contentBaseName = Split-Path $_ -LeafBase
            $targetPath = Join-Path $targetDirectory "$OutputPrefix$contentBaseName.md"
            GenerateMarkdown $_ | Set-Content $targetPath

            Write-Output (Get-Item $targetPath)
        }
}

# TODO TandokuVersionControlAdd for modified files in $OutputPath
# it is a useful part of my workflow though to add files to git staging, run a command, and diff any changes
# against the staged files, so consider adding an override parameter to skip adding to version control