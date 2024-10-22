param(
    # TODO - make this optional to allow analyzing all images as well?
    [Parameter(Mandatory=$true)]
    [String]
    $ContentPath,

    [Parameter(Mandatory=$true)]
    [ValidateSet('acv4','easyocr')]
    [String]
    $Provider,

    [Parameter()]
    $Volume
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local

function GetImagesToAnalyze($contentFilePath) {
    $content = ImportYaml $contentFilePath

    foreach ($block in $content) {
        $image = $block.image.name
        if ($image -and (-not $block.chunks.text)) {
            "$imagesPath/$image"
        }
    }
}

$Volume = ResolveVolume $Volume
if (-not $Volume) {
    return
}
$volumePath = $Volume.Path
$imagesPath = "$volumePath/images"

$contentFiles = Get-ChildItem $ContentPath -Filter *.content.yaml
if (-not $contentFiles) {
    Write-Warning "No content files found in $ContentPath, nothing to do"
    return
}

$imageFiles = $contentFiles | ForEach-Object { GetImagesToAnalyze $_ }

$outputItems = switch ($Provider) {
    'acv4' {
        TandokuImagesAnalyze_acv4 $imageFiles -Volume $volume
    }
    'easyocr' {
        TandokuImagesAnalyze_easyocr $imageFiles -Volume $volume
    }
}

if ($outputItems) {
    TandokuVersionControlAdd -Path $imagesPath -Kind binary
}