[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [Parameter()]
    [string]$OutputPath = 'films.json',

    [string]$LogPath
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/../../modules/tandoku-log.psm1"

# When -LogPath is supplied, additionally record warnings and errors (including
# uncaught terminating errors) to that file. See tandoku-log.psm1.
Initialize-TandokuLog -LogPath $LogPath
trap { Write-TandokuLogEntry 'ERROR' $_; break }

$films = @(Import-Yaml -LiteralPath $DatabasePath)

Write-Host "Read $($films.Count) entries from films database"

$films | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath

Write-Host "Exported to $OutputPath"
