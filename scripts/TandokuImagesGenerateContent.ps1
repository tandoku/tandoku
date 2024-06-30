param(
    [Parameter()]
    [String]
    $InputPath,

    [Parameter()]
    [String]
    $OutputPath,

    [Parameter()]
    [String]
    $SplitByFileName,

    # TODO - $MaxBlocksPerFile

    [Parameter(Mandatory=$true)]
    [ValidateSet('acv4','easyocr')]
    [String]
    $Provider,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function GenerateBlocksFromOcrText {
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [String]
        $ImagePath
    )
    process {
        # TODO - should really share this with AddAcvText in TandokuImagesAnalyze.ps1
        $source = [IO.FileInfo](Convert-Path $ImagePath)
        $textDir = (Join-Path $source.Directory 'text')
        $ocrPath = (Join-Path $textDir "$([IO.Path]::GetFilenameWithoutExtension($source.Name)).$Provider.json")
        if (Test-Path $ocrPath) {
            $ocr = Get-Content $ocrPath | ConvertFrom-Json

            $rootBlock = @{
                image = @{
                    name = $source.Name
                }
                blocks = @(ReadBlocksFromOcr $ocr)
            }

            if ($rootBlock.blocks.Count -gt 0) {
                if ($rootBlock.blocks.Count -eq 1) {
                    $block = $rootBlock.blocks[0]
                    $rootBlock.Remove('blocks')
                    # TODO - generalize this (merge hashtables with nesting support)
                    if ($rootBlock.image -and $block.image) {
                        $rootBlock.image += $block.image
                        $block.Remove('image')
                    }
                    $rootBlock += $block
                }

                $result = @{
                    FileName = $source.Name
                    Block = $rootBlock
                }
                Write-Output $result
            }
        } else {
            Write-Warning "Skipping $ImagePath because $ocrPath is missing"
        }
    }
}

function ReadBlocksFromOcr($ocr) {
    switch ($Provider) {
        'acv4' {
            return @($ocr.readResult.blocks.lines | ForEach-Object {
                return @{
                    text  = $_.text
                    image = @{
                        region = @{
                            segments = @($_.words | Select-Object text, confidence)
                        }
                    }
                }
            })
        }
        'easyocr' {
            return @($ocr.readResult | ForEach-Object {
                return @{
                    text  = $_.text
                    image = @{
                        region = @{
                            segments = @(
                                @{
                                    text = $_.text
                                    confidence = $_.confident
                                }
                            )
                        }
                    }
                }
            })
        }
        default {
            throw "Unexpected provider '$Provider'"
        }
    }
}

# TODO - consider factoring this out and reusing in TandokuCsvGenerateContent
function SaveContentBlocks {
    param(
        [Parameter(Mandatory=$true)]
        $Path,

        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        $BlockResult
    )
    begin {
        $currentContentBaseName = $null
    }
    process {
        $fileName = $BlockResult.FileName
        $block = $BlockResult.Block

        if ($SplitByFileName -and $fileName -match $SplitByFileName) {
            $contentBaseName = $Matches.Count -gt 1 ? $Matches[1] : $Matches[0]
        } else {
            $contentBaseName = ''
        }

        if ($contentBaseName -ne $currentContentBaseName) {
            if ($writer) {
                $writer.Close()
                Write-Output $contentPath
            }
            $currentContentBaseName = $contentBaseName
            $contentFileName = $contentBaseName ?
                "$contentBaseName.content.yaml" :
                'content.yaml'
            $contentPath = "$Path/$contentFileName"
            CreateDirectoryIfNotExists $Path
            $writer = [IO.File]::CreateText($contentPath)
        }

        $writer.WriteLine((ConvertTo-Yaml $block).TrimEnd())
        $writer.WriteLine('---')
    }
    end {
        if ($writer) {
            $writer.Close()
            Write-Output $contentPath
        }
    }
}

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

if (-not $InputPath) {
    $InputPath = "$volumePath/images"
}
if (-not $OutputPath) {
    $OutputPath = "$volumePath/content"
}

$imageExtensions = GetImageExtensions
$images = @()
foreach ($imageExtension in $imageExtensions) {
    $images += Get-ChildItem -Path $InputPath -Filter "*$imageExtension"
}

$outputItems = $images |
    WritePipelineProgress -Activity 'Generating content' -ItemName 'image' -TotalCount $images.Count |
    GenerateBlocksFromOcrText |
    SaveContentBlocks $OutputPath

if ($outputItems) {
    TandokuVersionControlAdd -Path $outputItems -Kind text
}