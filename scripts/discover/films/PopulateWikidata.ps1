[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath
)

Import-Module "$PSScriptRoot\..\..\modules\tandoku-yaml.psm1"

# Read existing films database
$films = [System.Collections.Generic.List[object]]::new()
foreach ($doc in @(Import-Yaml -LiteralPath $DatabasePath)) {
    $films.Add($doc)
}

Write-Host "Read $($films.Count) entries from films database"

# Find entries missing wikidata that have a Netflix ID
$needsLookup = @{}
foreach ($film in $films) {
    if (-not $film.wikidata -and $film.providers -and $film.providers.netflix -and $null -ne $film.providers.netflix.id) {
        $needsLookup[[string]$film.providers.netflix.id] = $film
    }
}

if ($needsLookup.Count -eq 0) {
    Write-Host "No entries need Wikidata lookup"
    return
}

Write-Host "$($needsLookup.Count) entries need Wikidata lookup"

# Batch query Wikidata SPARQL for Netflix ID -> Wikidata QID mappings
$batchSize = 50
$netflixIds = @($needsLookup.Keys)
$matched = 0

for ($i = 0; $i -lt $netflixIds.Count; $i += $batchSize) {
    $end = [Math]::Min($i + $batchSize - 1, $netflixIds.Count - 1)
    $batch = $netflixIds[$i..$end]
    $values = ($batch | ForEach-Object { "`"$_`"" }) -join " "
    $query = "SELECT ?item ?netflixId WHERE { VALUES ?netflixId { $values } ?item wdt:P1874 ?netflixId . }"

    $url = "https://query.wikidata.org/sparql?query=$([uri]::EscapeDataString($query))&format=json"

    try {
        $result = Invoke-RestMethod -Uri $url -Headers @{
            "User-Agent" = "tandoku-discover/1.0 (https://github.com/tandoku)"
        }
        foreach ($binding in $result.results.bindings) {
            $qid = $binding.item.value -replace '.*/entity/', ''
            $netflixId = $binding.netflixId.value
            $needsLookup[$netflixId]['wikidata'] = $qid
            $matched++
        }
    }
    catch {
        Write-Warning "Failed to query Wikidata for batch starting at index ${i}: $_"
    }

    Write-Host "Queried Wikidata: $([Math]::Min($i + $batchSize, $netflixIds.Count))/$($netflixIds.Count)"

    # Brief delay between batches to respect rate limits
    if ($i + $batchSize -lt $netflixIds.Count) {
        Start-Sleep -Milliseconds 1000
    }
}

# Write films.yaml
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $matched/$($needsLookup.Count) entries matched to Wikidata"
