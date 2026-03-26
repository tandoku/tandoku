[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path,

    [Parameter(Mandatory)]
    [string]$DatabasePath
)

Import-Module powershell-yaml

# Read Netflix watchlist
$watchlist = Get-Content -Path $Path -Raw | ConvertFrom-Json

Write-Host "Read $($watchlist.Count) items from Netflix watchlist"

# Read existing films database
$films = [System.Collections.Generic.List[object]]::new()
if (Test-Path $DatabasePath) {
    $yamlContent = Get-Content -Path $DatabasePath -Raw
    if ($yamlContent -and $yamlContent.Trim()) {
        foreach ($doc in @(ConvertFrom-Yaml -Yaml $yamlContent -AllDocuments)) {
            $films.Add($doc)
        }
    }
}

Write-Host "Read $($films.Count) existing entries from films database"

# Build lookup tables for existing films
$filmsByWikidata = @{}
$filmsByNetflixId = @{}
for ($i = 0; $i -lt $films.Count; $i++) {
    $film = $films[$i]
    if ($film.wikidata) {
        $filmsByWikidata[$film.wikidata] = $i
    }
    if ($film.providers -and $film.providers.netflix -and $null -ne $film.providers.netflix.id) {
        $filmsByNetflixId[[string]$film.providers.netflix.id] = $i
    }
}

# Batch query Wikidata SPARQL for Netflix ID -> Wikidata QID mappings
$batchSize = 50
$netflixToWikidata = @{}
$watchlistVideoIds = @($watchlist | ForEach-Object { $_.videoId })

for ($i = 0; $i -lt $watchlistVideoIds.Count; $i += $batchSize) {
    $end = [Math]::Min($i + $batchSize - 1, $watchlistVideoIds.Count - 1)
    $batch = $watchlistVideoIds[$i..$end]
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
            $netflixToWikidata[$netflixId] = $qid
        }
    }
    catch {
        Write-Warning "Failed to query Wikidata for batch starting at index ${i}: $_"
    }

    Write-Host "Queried Wikidata: $([Math]::Min($i + $batchSize, $watchlistVideoIds.Count))/$($watchlistVideoIds.Count)"

    # Brief delay between batches to respect rate limits
    if ($i + $batchSize -lt $watchlistVideoIds.Count) {
        Start-Sleep -Milliseconds 1000
    }
}

Write-Host "Matched $($netflixToWikidata.Count)/$($watchlistVideoIds.Count) Netflix items to Wikidata entities"

# Track which Netflix IDs are in the current watchlist
$watchlistNetflixIds = [System.Collections.Generic.HashSet[string]]::new()
foreach ($item in $watchlist) {
    [void]$watchlistNetflixIds.Add($item.videoId)
}

# Process each Netflix watchlist item
$added = 0
$updated = 0
foreach ($item in $watchlist) {
    $videoId = $item.videoId
    $qid = $netflixToWikidata[$videoId]

    if (-not $qid) {
        Write-Warning "No Wikidata match for '$($item.title)' (Netflix ID: $videoId) - skipping"
        continue
    }

    # Find existing entry by Wikidata QID (primary) or Netflix ID (fallback)
    $existingIndex = $null
    if ($filmsByWikidata.ContainsKey($qid)) {
        $existingIndex = $filmsByWikidata[$qid]
    } elseif ($filmsByNetflixId.ContainsKey($videoId)) {
        $existingIndex = $filmsByNetflixId[$videoId]
    }

    if ($null -ne $existingIndex) {
        # Update existing entry
        $film = $films[$existingIndex]
        $film['wikidata'] = $qid
        if (-not $film.Contains('providers')) {
            $film['providers'] = [ordered]@{}
        }
        $film['providers']['netflix'] = [ordered]@{
            id        = [int]$videoId
            title     = $item.title
            watchlist = $true
        }
        $updated++
    } else {
        # Add new entry
        $newFilm = [ordered]@{
            wikidata  = $qid
            providers = [ordered]@{
                netflix = [ordered]@{
                    id        = [int]$videoId
                    title     = $item.title
                    watchlist = $true
                }
            }
        }
        $newIndex = $films.Count
        $films.Add($newFilm)
        $filmsByWikidata[$qid] = $newIndex
        $filmsByNetflixId[$videoId] = $newIndex
        $added++
    }
}

# Mark removed watchlist items (in DB but no longer in imported watchlist)
$unmarked = 0
foreach ($film in $films) {
    if ($film.providers -and $film.providers.netflix -and $film.providers.netflix.watchlist -eq $true) {
        $netflixId = [string]$film.providers.netflix.id
        if (-not $watchlistNetflixIds.Contains($netflixId)) {
            $film.providers.netflix.watchlist = $false
            $unmarked++
        }
    }
}

# Write films.yaml
$yamlDocs = @($films | ForEach-Object { (ConvertTo-Yaml $_).TrimEnd() })
$outputYaml = ($yamlDocs -join "`n---`n") + "`n"
Set-Content -Path $DatabasePath -Value $outputYaml -NoNewline

Write-Host "Done: $added added, $updated updated, $unmarked unmarked - $($films.Count) total entries in $DatabasePath"
