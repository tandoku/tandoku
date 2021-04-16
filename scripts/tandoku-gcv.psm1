#TODO: when starting tandoku environment, add the scripts directory to $env:PSModulePath so modules are auto-loaded
#consider choosing another name for Sort-STNumerical since Sort is a reserved verb
Import-Module (Join-Path (Split-Path $MyInvocation.MyCommand.Path -Parent) 'tandoku-utils.psm1') -DisableNameChecking

#choco install gcloudsdk #may not work due to hash not matching
$env:GOOGLE_APPLICATION_CREDENTIALS=(Convert-Path 'O:\Tandoku\Tools\tandoku-test1-1c46e530ca99.json')

#ls image*.jpeg -Recurse|Add-GcvOcr
function Add-GcvOcr {
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [ValidateScript({
            if( -Not ($_ | Test-Path) ){
                throw "File or folder does not exist"
            }
            return $true
        })]
        [String]
        $Path
    )
    process {
        $source = [IO.FileInfo](Convert-Path $Path)
        $ocrDir = (Join-Path $source.Directory 'ocr')
        if (-not (Test-Path $ocrDir)) {
            [void] (New-Item -Type Directory $ocrDir)
        }
        $target = (Join-Path $ocrDir "$([IO.Path]::GetFilenameWithoutExtension($source.Name)).gcv.json")
        if (-not (Test-Path $target)) {
            $base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($source))
            $body = @{
                requests = @(@{
                    image = @{
                        content = $base64
                    }
                    features = @(@{
                        type = 'DOCUMENT_TEXT_DETECTION'
                    })
                    imageContext = @{
                        languageHints = @('ja')
                    }
                })
            }
            $bodyJson = (ConvertTo-Json $body -Depth 10)

            do {
                $retryWebRequest = $false

                if (-not ($script:gcloudAuthHeaders)) {
                    $cred = gcloud auth application-default print-access-token
                    $script:gcloudAuthHeaders = @{ "Authorization" = "Bearer $cred" }
                }
                
                try {
                    Invoke-WebRequest `
                      -Method POST `
                      -Headers $script:gcloudAuthHeaders `
                      -ContentType: "application/json; charset=utf-8" `
                      -Body $bodyJson `
                      -OutFile $target `
                      -MaximumRetryCount 3 `
                      -RetryIntervalSec 2 `
                      -Uri "https://vision.googleapis.com/v1/images:annotate"

                    if (Test-Path $target) {
                        $target
                    } else {
                        Write-Error "Failed to create $target"
                    }
                } catch {
                    if ($_.exception.response.statuscode -eq 'Unauthorized') {
                        $script:gcloudAuthHeaders = $null
                        $retryWebRequest = $true
                    } else {
                        throw
                    }
                }
            } while ($retryWebRequest)
        }
    }
}

function Import-GcvOcrContent {
    Get-ChildItem images -Filter *.gcv.json -Recurse |
        Get-Content |
        ConvertFrom-Json
}

function Get-GcvOcrContentForImage($imagePath) {
    if (-not $imagePath) {
        $imagePath = [String] (Get-Clipboard)
        $imagePath = $imagePath.Trim('"')
    }

    #TODO: factor out function to get ocr path from image path (use in Add-GcvOcr as well)
    $gcvOcrFilename = [IO.Path]::GetFileName([IO.Path]::ChangeExtension($imagePath, '.gcv.json'))
    $gcvOcrPath = [IO.Path]::Combine([IO.Path]::GetDirectoryName($imagePath), 'ocr')
    $gcvOcrPath = [IO.Path]::Combine($gcvOcrPath, $gcvOcrFilename)

    if (-not (Test-Path $gcvOcrPath)) {
        Write-Error "Path does not exist: $gcvOcrPath"
        return
    }

    $json = Get-Content -LiteralPath $gcvOcrPath | ConvertFrom-Json
    $json.responses[0].fullTextAnnotation.text
}

function Export-GcvOcrToMarkdown {
    Get-ChildItem images -Filter *.gcv.json -Recurse |
        Sort-STNumerical |
        Foreach-Object {
            $baseName = [IO.Path]::GetFileNameWithoutExtension([IO.Path]::GetFileNameWithoutExtension($_))
            $imagePath = "images/$baseName.jpeg"
            Write-Output "![]($imagePath)"
            Write-Output ''

            #Write-Output "# $baseName"
            Write-Output ''

            $gcv = Get-Content $_ | ConvertFrom-Json
#TODO:
#- add breaks (space, line break)
#- filter out low-confidence blocks and/or paragraphs (<0.5)
#- filter out paragraphs with no kana/kanji (could also pre-filter to blocks/paragraphs with detected language != ja or zh)
#- add italics to low-confidence symbols (<0.5)
#- filter out furigana... (need to look at bounding boxes for this)
            $gcv.responses.fullTextAnnotation.pages.blocks.paragraphs |
                Foreach-Object {
                    Write-Output (-join $_.words.symbols.text)
                    Write-Output ''
                }
        }
}

function Get-GcvOcrWordCount {
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [ValidateScript({
            if( -Not ($_ | Test-Path) ){
                throw "File or folder does not exist"
            }
            return $true
        })]
        [String]
        $Path
    )
    begin {
        $n = 0
    }
    process {
        $gcv = (Get-Content $Path | ConvertFrom-Json)
        $n += $gcv.responses.fullTextAnnotation.pages.blocks.paragraphs.words.count
    }
    end {
        $n
    }
}

Export-ModuleMember -Function * -Alias *
