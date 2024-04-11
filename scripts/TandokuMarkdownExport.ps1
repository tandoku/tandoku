param(
    [Parameter()]
    [String]
    $VolumePath,

    [Parameter()]
    [String]
    [ValidateSet('None', 'Footnotes', 'BlurredText')]
    $ReferenceTextStyle = 'None'
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function GenerateMarkdown($contentPath, $targetDirectory) {
    $content = Get-Content $contentPath | ConvertFrom-Yaml -AllDocuments
    $footnote = 0
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
            $imagePath = "images/$imageName"
            # TODO - factor this out (try using Uri class)
            $imageUrl = $imagePath.Replace('(', '%28').Replace(')', '%29').Replace(' ', '%20')
            Write-Output "![$heading]($imageUrl)"
            Write-Output ''
        }

        # Text
        $blockText = $block.text
        $blockRefText = $block.references.en # TODO - should be references.en.text
        if ($blockRefText) {
            $footnote += 1
            if ($ReferenceTextStyle -eq 'Footnotes') {
                Write-Output "$blockText [^$footnote]"
                Write-Output ''
                Write-Output "[^$footnote]: $blockRefText"
                Write-Output ''
            } elseif ($ReferenceTextStyle -eq 'BlurredText') {
                # references
                # - https://www.w3docs.com/snippets/css/how-to-create-a-blurry-text-in-css.html
                # - https://bernholdtech.blogspot.com/2013/04/very-simple-pure-css-collapsible-list.html

                $blockRefHtml = (ConvertFrom-Markdown -InputObject $blockRefText).Html
                $blockRefHtml = $blockRefHtml -replace '^<p>',"<p class='blurText'><label for='ref-en-$footnote'>"
                $blockRefHtml = $blockRefHtml -replace '</p>$',"</label><input type='checkbox' id='ref-en-$footnote'/></p>"

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

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

# TODO - add this as another property on volume info
# also consider dropping the moniker from this (just the cleaned title)
# Note - this is only needed when producing single merged .md file
$volumeBaseFileName = Split-Path $volumePath -Leaf

$targetDirectory = Join-Path $volumePath 'markdown'
if ($ReferenceTextStyle -eq 'Footnotes') {
    $targetDirectory = Join-Path $targetDirectory 'footnotes'
} elseif ($ReferenceTextStyle -eq 'BlurredText') {
    $targetDirectory = Join-Path $targetDirectory 'blurrefs'
}
CreateDirectoryIfNotExists $targetDirectory
$targetPath = Join-Path $targetDirectory "$volumeBaseFileName.md"

$contentFiles =
    @(Get-ChildItem "$volumePath/content" -Filter content.yaml) +
    @(Get-ChildItem "$volumePath/content" -Filter *.content.yaml)

# TODO - Split/NoMerge option to generate .md file for each content file
# (put this in a [split-] or [nomerge-] directory)
$contentFiles |
    Foreach-Object { GenerateMarkdown $_ $targetDirectory } |
    Set-Content $targetPath