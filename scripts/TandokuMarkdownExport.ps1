param(
    [Parameter()]
    [String]
    $VolumePath,

    [Parameter()]
    [Switch]
    $Combine,

    [Parameter()]
    [String]
    [ValidateSet('None', 'Footnotes', 'BlurHtml')]
    $ReferenceBehavior = 'None'
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function GenerateMarkdown($contentPath) {
    $content = Get-Content $contentPath | ConvertFrom-Yaml -AllDocuments
    $refIndex = 0 # TODO - this is wrong if $Combine, should not reset for each file
    foreach ($block in $content) {
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

        # Text
        $blockText = $block.text
        $blockRefText = $block.references.en.text
        if ($blockRefText) {
            $refIndex += 1
            if ($ReferenceBehavior -eq 'Footnotes') {
                Write-Output "$blockText [^$refIndex]"
                Write-Output ''
                Write-Output "[^$refIndex]: $blockRefText"
                Write-Output ''
            } elseif ($ReferenceBehavior -eq 'BlurHtml') {
                # references
                # - https://www.w3docs.com/snippets/css/how-to-create-a-blurry-text-in-css.html
                # - https://bernholdtech.blogspot.com/2013/04/very-simple-pure-css-collapsible-list.html

                $blockRefHtml = (ConvertFrom-Markdown -InputObject $blockRefText).Html
                $blockRefHtml = $blockRefHtml -replace '^<p>',"<p class='blurText'><input type='checkbox' id='ref-en-$refIndex'/><label for='ref-en-$refIndex'>"
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