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

    [Parameter()]
    [ValidateSet('None', 'Html', 'BlurHtml', 'Remove')]
    [String]
    $RubyBehavior = 'None',

    [Parameter()]
    [ValidateSet('None', 'Footnotes', 'BlurHtml')]
    [String]
    $ReferenceBehavior = 'None',

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function GenerateMarkdown($contentPath) {
    $content = Get-Content $contentPath | ConvertFrom-Yaml -AllDocuments -Ordered

    # Note that a unique id prefix is used anytime there are multiple input files, whether or not the output
    # will be combined into a single markdown file.
    # Required by TandokuEpubExport which treats multiple markdown files as a single concatenated file.
    $idPrefix = GetContentBaseName $contentPath
    if (-not $idPrefix) {
        # Default id prefix - blockId may be used in HTML id attributes which must start with a letter
        $idPrefix = 'block'
    }

    $blockIndex = 0
    foreach ($block in $content) {
        $blockIndex += 1
        $blockId = "$idPrefix-$blockIndex"

        # TODO - generalize this (markdown templates)

        # Heading
        $heading = GetHeading $block
        if ($heading) {
            Write-Output "# $heading"
            Write-Output ''
        }

        # Image
        $imageName = $block.image.name
        if ($imageName) {
            $imageNameEncoded = [Uri]::EscapeDataString($imageName)
            $imageUrl = "images/$imageNameEncoded"
            Write-Output "![$heading]($imageUrl)"
            Write-Output ''
        }

        if ($block.blocks) {
            $nestedBlockIndex = 0
            foreach ($nestedBlock in $block.blocks) {
                $nestedBlockIndex += 1
                $nestedBlockId = "$blockId-$nestedBlockIndex"
                GenerateMarkdownForTextBlock $nestedBlock $nestedBlockId
            }
        } else {
            GenerateMarkdownForTextBlock $block $blockId
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

function GenerateMarkdownForTextBlock($block, $blockId) {
    # Text / Reference text

    $blockText = ProcessRubyText $block.text
    if ($RubyBehavior -eq 'BlurHtml') {
        $blockText = ConvertTextToBlurHtml $blockText $blockId -Ruby
    }

    $blockRefTextBuilder = [Text.StringBuilder]::new()
    foreach ($refName in $block.references.Keys) {
        $ref = $block.references[$refName]
        $blockRefText = $ref.text
        if ($blockRefText) {
            $blockRefId = "$blockId-ref-$refName"

            switch ($ReferenceBehavior) {
                'Footnotes' {
                    $blockText = "$blockText [^$blockRefId]"
                    $blockRefText = "[^$blockRefId]: $blockRefText"
                }
                'BlurHtml' {
                    $blockRefText = ConvertTextToBlurHtml $blockRefText $blockRefId
                }
            }

            if ($ReferenceBehavior -ne 'Footnotes') {
                $blockRefText = "$($refName): $blockRefText"
            }

            [void] $blockRefTextBuilder.AppendLine($blockRefText)
            [void] $blockRefTextBuilder.AppendLine()
        }
    }

    Write-Output $blockText
    Write-Output ''
    if ($blockRefTextBuilder.Length -gt 0) {
        Write-Output $blockRefTextBuilder
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

function ConvertTextToBlurHtml([String]$text, [String]$id, [Switch]$Ruby) {
    # references
    # - https://www.w3docs.com/snippets/css/how-to-create-a-blurry-text-in-css.html
    # - https://bernholdtech.blogspot.com/2013/04/very-simple-pure-css-collapsible-list.html

    if ($Ruby -and (-not $text.Contains('<ruby>'))) {
        return $text
    }

    $blurClass = $Ruby ? 'blurRuby' : 'blurText'

    $html = (ConvertFrom-Markdown -InputObject $text).Html
    $html = $html -replace '^<p>',"<p class='$blurClass'><input type='checkbox' id='$id'/><label for='$id'>"
    $html = $html -replace '</p>$',"</label></p>"
    return $html.TrimEnd()
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
