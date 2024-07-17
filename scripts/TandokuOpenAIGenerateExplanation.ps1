param(
    [Parameter(Mandatory=$true)]
    [String]
    $InputPath,

    [Parameter(Mandatory=$true)]
    [String]
    $OutputPath,

    [Parameter()]
    [String]
    $VolumePath
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

function InitOpenAI {
    $apiKeyConfig = 'openai.apiKey'
    $script:openaiApiKey = TandokuConfig $apiKeyConfig

    if (-not $script:openaiApiKey) {
        throw "Missing required configuration, set $apiKeyConfig in tandoku config"
    }
}

<#
function GenerateExplanations {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Path
    )
    $contentBlocks = Get-Content $Path | ConvertFrom-Yaml -AllDocuments -Ordered
}

function GenerateExplanation {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Text
    )
    $headers = @{
        'Content-Type' = 'application/json'
        'Authorization' = "Bearer $script:openaiApiKey"
    }
    $body = @{
        model = 'gpt-4o'
        temperature = 0.3
        max_tokens = 4000
        top_p = 1
        frequency_penalty = 0
        presence_penalty = 0
        messages = @(
            @{
                role = 'system'
                content = @(
                    @{
                        type = 'text'
                        text = Get-Content "$PSScriptRoot/../resources/prompts/explanation.txt"
                    }
                )
            },
            @{
                role = 'user'
                content = @(
                    @{
                        type = 'text'
                        text = $Text
                    }
                )
            }
        )
    }
    $response = Invoke-RestMethod -Uri https://api.openai.com/v1/chat/completions -Method POST -Headers $headers -Body $body
    $responseText = $response.choices[0].text
    return ($responseText | ConvertFrom-Yaml -AllDocuments)
}
#>

$volume = TandokuVolumeInfo -VolumePath $VolumePath
if (-not $volume) {
    return
}
$volumePath = $volume.path

InitOpenAI

$env:OPENAI_API_KEY = $script:openaiApiKey
tandoku content transform generate-explanation $InputPath $OutputPath

<#
$contentFiles =
    @(Get-ChildItem $InputPath -Filter content.yaml) +
    @(Get-ChildItem $InputPath -Filter *.content.yaml)
if (-not $contentFiles) {
    Write-Warning "No content files found in $contentDirectory, nothing to do"
    return
}

CreateDirectoryIfNotExists $OutputPath

$contentFiles | Foreach-Object {
    $contentFileName = Split-Path $_ -Leaf
    $targetPath = Join-Path $OutputPath $contentFileName
    GenerateExplanation $_ | Set-Content $targetPath

    Write-Output (Get-Item $targetPath)
}
#>