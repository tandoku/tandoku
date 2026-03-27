[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [Parameter()]
    [string]$OutputPath = 'films.json'
)

Import-Module "$PSScriptRoot\..\..\modules\tandoku-yaml.psm1"

$films = @(Import-Yaml -LiteralPath $DatabasePath)

Write-Host "Read $($films.Count) entries from films database"

$films | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath

Write-Host "Exported to $OutputPath"
