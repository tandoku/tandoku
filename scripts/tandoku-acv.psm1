function Add-AcvOcr {
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
        $target = (Join-Path $ocrDir "$([IO.Path]::GetFilenameWithoutExtension($source.Name)).acv.json")
        if (-not (Test-Path $target)) {
            if (-not ($script:acvApiKey)) {
                $script:acvApiKey = (Get-Content O:\Tandoku\Tools\tandoku-azure-vision.txt)
            }
                
            $body = [IO.File]::ReadAllBytes($source)
            $headers = @{
                'Content-Type' = 'image/jpeg'
                'Ocp-Apim-Subscription-Key' = $script:acvApiKey
            }

            $response = Invoke-WebRequest `
              -Method POST `
              -Headers $headers `
              -ContentType: "application/octet-stream" `
              -Body $body `
              -MaximumRetryCount 3 `
              -RetryIntervalSec 2 `
              -Uri "https://westus2.api.cognitive.microsoft.com/vision/v3.2-preview.3/read/analyze?language=ja"

            if ($response.StatusCode -eq 202) {
                $resultUri = [string]$response.Headers.'Operation-Location'
                $headers = @{
                    'Ocp-Apim-Subscription-Key' = $script:acvApiKey
                }

                do {
                    Start-Sleep 3100

                    $response = Invoke-WebRequest `
                      -Method GET `
                      -Headers $headers `
                      -MaximumRetryCount 3 `
                      -RetryIntervalSec 2 `
                      -Uri $resultUri

                    $responseJson = $response | ConvertFrom-Json
                    if ($responseJson.status -eq 'succeeded') {
                        $response.Content | Out-File $target
                    }
                } while ($responseJson.status -in 'notStarted','running')

                if ($responseJson -and $responseJson.status -eq 'failed') {
                    Write-Error "Azure OCR failed: $responseJson"
                }
            }

            if (Test-Path $target) {
                $target
            } else {
                Write-Error "Failed to create $target"
            }
        }
    }
}

function Export-AcvOcrToMarkdown {
    Get-ChildItem images -Filter *.acv.json -Recurse |
        Sort-STNumerical |
        Foreach-Object {
            $baseName = [IO.Path]::GetFileNameWithoutExtension([IO.Path]::GetFileNameWithoutExtension($_))
            $imagePath = "images/$baseName.jpeg"
            Write-Output "![]($imagePath)"
            Write-Output ''

            #Write-Output "# $baseName"
            Write-Output ''

            $acv = Get-Content $_ | ConvertFrom-Json
            $acv.analyzeResult.readResults.lines |
                Foreach-Object {
                    Write-Output $_.text
                    Write-Output ''
                }
        }
}

#TODO: sort lines correctly, remove furigana
#for furigana, first construct blocks/paragraphs (group of enlarged bounding boxes that intersect) and then complete lines
#then we can identify lines that contain only kana and determine whether each character is significantly smaller than the
#character in the adjacent line
#also handle unexpected kanji+furigana 'lines' from result by detecting horizontal text with kanji+kana within otherwise
#vertical text
#$acv.analyzeResult.readResults.lines|Sort-Object -Property @{Expression={$_.boundingBox[0]}} -Descending
#$acv.analyzeResult.readResults.lines|Sort-Object -Property @{Expression={$_.boundingBox[0]}} -Descending|Select-Object -Property @{Expression={$_.boundingBox[4] - $_.boundingBox[0]};Name='width'},*|? width -lt 100|Sort-Object -Property width -Descending
