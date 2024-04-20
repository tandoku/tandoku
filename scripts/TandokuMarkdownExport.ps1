param(
    [Parameter()]
    [String]
    $VolumePath,

    [Parameter()]
    [Switch]
    $Combine,

    [Parameter()]
    [ValidateSet('None', 'Expand', 'Remove')]
    [String]
    $RubyBehavior = 'None', # TODO - this should probably be a separate transform step

    [Parameter()]
    [ValidateSet('None', 'Footnotes', 'BlurHtml')]
    [String]
    $ReferenceBehavior = 'None'
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
        $heading = $block.source.block
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
        $blockRefText = $block.references.en.text
        if ($blockRefText) {
            $refId = "ref-en-$blockIndex"
            if ($idPrefix) {
                $refId = "$idPrefix-$refId"
            }
            if ($ReferenceBehavior -eq 'Footnotes') {
                Write-Output "$blockText [^$refId]"
                Write-Output ''
                Write-Output "[^$refId]: $blockRefText"
                Write-Output ''
            } elseif ($ReferenceBehavior -eq 'BlurHtml') {
                # references
                # - https://www.w3docs.com/snippets/css/how-to-create-a-blurry-text-in-css.html
                # - https://bernholdtech.blogspot.com/2013/04/very-simple-pure-css-collapsible-list.html

                $blockRefHtml = (ConvertFrom-Markdown -InputObject $blockRefText).Html
                $blockRefHtml = $blockRefHtml -replace '^<p>',"<p class='blurText'><input type='checkbox' id='$refId'/><label for='$refId'>"
                $blockRefHtml = $blockRefHtml -replace '</p>$',"</label></p>"

                Write-Output $blockText
                Write-Output ''
                Write-Output $blockRefHtml
            } else {
                Write-Output $blockText
                Write-Output ''
                Write-Output $blockRefText
                Write-Output ''
            }
        } else {
            Write-Output $blockText
            Write-Output ''
        }
    }
}

function ProcessRubyText($text) {
    if ($RubyBehavior -ne 'None') {
        $rubyMatch = '(^| )([^ \[]+)\[(.+?)\]'
        $rubyReplace = switch ($RubyBehavior) {
            'Expand' { '<ruby><rb>$2</rb><rt>$3</rt></ruby>' }
            'Remove' { '$2' }
        }
        return $text -replace $rubyMatch,$rubyReplace
    }
    return $text
}

function GetTargetDirectory($volumePath) {
    $targetDirectory = Join-Path $volumePath 'markdown'

    $tags = @()
    if ($Combine) {
        $tags += "combined"
    }
    if ($ReferenceBehavior -ne 'None') {
        $tags += "ref-$($ReferenceBehavior.ToLowerInvariant())"
    }
    if ($tags) {
        $targetDirectory = Join-Path $targetDirectory ($tags -join '-')
    }

    return $targetDirectory
}

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$targetDirectory = GetTargetDirectory $volumePath
CreateDirectoryIfNotExists $targetDirectory

$contentFiles =
    @(Get-ChildItem "$volumePath/content" -Filter content.yaml) +
    @(Get-ChildItem "$volumePath/content" -Filter *.content.yaml)

if ($Combine) {
    # TODO - add this as another property on volume info
    # also consider dropping the moniker from this (just the cleaned title)
    $volumeBaseFileName = Split-Path $volumePath -Leaf
    $targetPath = Join-Path $targetDirectory "$volumeBaseFileName.md"

    $contentFiles |
        Foreach-Object { GenerateMarkdown $_ } |
        Set-Content $targetPath
} else {
    $contentFiles |
        Foreach-Object {
            $contentBaseName = Split-Path $_ -LeafBase
            $targetPath = Join-Path $targetDirectory "$contentBaseName.md"
            GenerateMarkdown $_ | Set-Content $targetPath
        }
}