param(
    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function InitAcv {
    $apiKeyConfig = 'azure-computer-vision.apiKey'
    $endpointConfig = 'azure-computer-vision.endpoint'
    $script:acvApiKey = TandokuConfig $apiKeyConfig
    $script:acvEndpoint = TandokuConfig $endpointConfig

    if ((-not $script:acvApiKey) -or (-not $script:acvEndpoint)) {
        throw "Missing required configuration, set $apiKeyConfig and $endpointConfig in tandoku config"
    }

    $script:acvEndpoint = $script:acvEndpoint.TrimEnd('/')
}

function AddAcvText {
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [String]
        $Path,

        [Parameter()]
        [String]
        $Language,

        [Parameter()]
        [Switch]
        $FreeTier
    )
    process {
        if ((-not $script:acvApiKey) -or (-not $script:acvEndpoint)) {
            throw 'Missing required variables, call InitAcv first'
        }

        $source = [IO.FileInfo](Convert-Path $Path)

        $textDir = (Join-Path $source.Directory 'text')
        CreateDirectoryIfNotExists $textDir

        $target = (Join-Path $textDir "$([IO.Path]::GetFilenameWithoutExtension($source.Name)).acv4.json")
        if (-not (Test-Path $target)) {
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

            $requestParams = $Language ? "&language=$Language" : $null
            $response = Invoke-WebRequest `
              -Method POST `
              -Headers $headers `
              -Body $body `
              -MaximumRetryCount 3 `
              -RetryIntervalSec $retryIntervalSec `
              -Uri "$script:acvEndpoint/computervision/imageanalysis:analyze?api-version=2024-02-01&features=read$requestParams" `
              -OutFile $target `
              -PassThru `
              -ProgressAction SilentlyContinue

            if ($response.StatusCode -ne 200) {
                Write-Error "Azure OCR failed: $response"
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

InitAcv

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

$path = "$VolumePath/images"
$imageExtensions = GetImageExtensions

$inputItems = @()
foreach ($imageExtension in $imageExtensions) {
    $inputItems += Get-ChildItem -Path $path -Filter "*$imageExtension"
}

$outputItems = $inputItems |
    WritePipelineProgress -Activity 'Recognizing text' -ItemName 'image' -TotalCount $inputItems.Count |
    AddAcvText -Language $volume.definition.language

if ($outputItems) {
    TandokuVersionControlAdd -Path $path -Kind binary
}