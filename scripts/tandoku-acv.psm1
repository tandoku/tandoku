function Add-AcvText {
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [ValidateScript({
            if( -Not ($_ | Test-Path) ){
                throw "File or folder does not exist"
            }
            return $true
        })]
        [String]
        $Path,

        [String]
        $Language = 'ja',

        [Switch]
        $FreeTier
    )
    process {
        $source = [IO.FileInfo](Convert-Path $Path)
        $textDir = (Join-Path $source.Directory 'text')
        if (-not (Test-Path $textDir)) {
            [void] (New-Item -Type Directory $textDir)
        }
        $target = (Join-Path $textDir "$([IO.Path]::GetFilenameWithoutExtension($source.Name)).acv.json")
        if (-not (Test-Path $target)) {
            if (-not ($script:acvApiKey)) {
                $script:acvApiKey = (Get-Content O:\Tandoku\Tools\tandoku-azure-vision.txt)
            }
                
            $body = [IO.File]::ReadAllBytes($source)
            $headers = @{
                'Content-Type' = (GetMimeType $source)
                'Ocp-Apim-Subscription-Key' = $script:acvApiKey
            }

            # Sleep between operations since free tier is limited to 20 calls per minute
            # (this isn't necessary if using S1 pricing tier)
            if ($FreeTier) {
                Write-Verbose "Waiting 3 seconds to avoid free tier request throttling..."
                Start-Sleep -Milliseconds 3100

                $retryIntervalSec = 4
            } else {
                $retryIntervalSec = 2
            }

            $response = Invoke-WebRequest `
              -Method POST `
              -Headers $headers `
              -ContentType: "application/octet-stream" `
              -Body $body `
              -MaximumRetryCount 3 `
              -RetryIntervalSec $retryIntervalSec `
              -Uri "https://tandoku.cognitiveservices.azure.com/vision/v3.2/read/analyze?language=$Language"

            if ($response.StatusCode -eq 202) {
                $resultUri = [string]$response.Headers.'Operation-Location'
                $headers = @{
                    'Ocp-Apim-Subscription-Key' = $script:acvApiKey
                }
                
                Write-Verbose "Resource location: $resultUri"

                do {
                    # Sleep between operations since free tier is limited to 20 calls per minute
                    # (this isn't necessary if using S1 pricing tier)
                    if ($FreeTier) {
                        Write-Verbose "Waiting 3 seconds to avoid free tier request throttling..."
                        Start-Sleep -Milliseconds 3100
                    }

                    Write-Verbose "Requesting resource..."
                    $response = Invoke-WebRequest `
                      -Method GET `
                      -Headers $headers `
                      -MaximumRetryCount 3 `
                      -RetryIntervalSec $retryIntervalSec `
                      -Uri $resultUri

                    $responseJson = $response | ConvertFrom-Json
                    Write-Verbose "Request status: $($responseJson.status)"
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

function GetMimeType($fileName) {
    $ext = [IO.Path]::GetExtension($fileName)
    switch ($ext) {
        '.jpg' {'image/jpeg'}
        '.jpeg' {'image/jpeg'}
        '.png' {'image/png'}
        '.tiff' {'image/tiff'}
    }
}

function Export-AcvTextToMarkdown {
    Get-ChildItem images -Filter *.acv.json -Recurse |
        Sort-STNumerical |
        Foreach-Object {
            $baseName = [IO.Path]::GetFileNameWithoutExtension([IO.Path]::GetFileNameWithoutExtension($_))
            if (Test-Path "images/$baseName.jpeg") {
                $imagePath = "images/$baseName.jpeg"
            } else {
                $imagePath = "images/$baseName.jpg"
            }
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
