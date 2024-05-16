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
    $content = Get-Content $contentPath | ConvertFrom-Yaml -AllDocuments

    # Note that an id prefix is used anytime there are multiple input files, whether or not the output
    # will be combined into a single markdown file.
    # Required by TandokuEpubExport which treats multiple markdown files as a single concatenated file.
    $idPrefix = GetContentBaseName $contentPath

    $blockIndex = 0
    foreach ($block in $content) {
        $blockIndex += 1

        # Heading
        # TODO - generalize this (markdown templates)
        $heading = $block.source.note ?? $block.source.resource
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

        # Text / Reference text
        $blockText = ProcessRubyText $block.text
        $blockId = $idPrefix ? "$idPrefix-$blockIndex" : "$blockIndex"
        if ($RubyBehavior -eq 'BlurHtml') {
            $blockText = ConvertTextToBlurHtml $blockText $blockId -Ruby
        }
        $blockRefText = $block.references.en.text
        if ($blockRefText) {
            $blockRefId = "ref-en-$blockIndex"
            if ($idPrefix) {
                $blockRefId = "$idPrefix-$blockRefId"
            }

            switch ($ReferenceBehavior) {
                'Footnotes' {
                    $blockText = "$blockText [^$blockRefId]"
                    $blockRefText = "[^$blockRefId]: $blockRefText"
                }
                'BlurHtml' {
                    $blockRefText = ConvertTextToBlurHtml $blockRefText $blockRefId
                }
            }

            Write-Output $blockText
            Write-Output ''
            Write-Output $blockRefText
            Write-Output ''
        } else {
            Write-Output $blockText
            Write-Output ''
        }
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
