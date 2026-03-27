[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath
)

Import-Module "$PSScriptRoot\..\..\modules\tandoku-yaml.psm1"

$sparqlHeaders = @{ "User-Agent" = "tandoku-discover/1.0 (https://github.com/tandoku)" }

# Preferred key order for film entries
$fieldOrder = @('wikidata', 'title', 'title-ja', 'type', 'originCountry', 'originalLanguage', 'year', 'imdb', 'myAnimeList', 'tmdb', 'providers')

# Wikidata type labels to exclude (not meaningful content types)
$excludedTypes = @('conflation', 'fictional crossover')

function Invoke-WikidataSparql($query) {
    $url = "https://query.wikidata.org/sparql?query=$([uri]::EscapeDataString($query))&format=json"
    $maxRetries = 3
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        try {
            return (Invoke-RestMethod -Uri $url -Headers $sparqlHeaders).results.bindings
        }
        catch {
            $retrySeconds = 0
            if ($_.Exception.Response.StatusCode -eq 429 -or $_ -match '429') {
                if ($_ -match 'retry in (\d+) seconds') {
                    $retrySeconds = [int]$Matches[1]
                } else {
                    $retrySeconds = 120
                }
            }

            if ($retrySeconds -gt 0 -and $attempt -lt $maxRetries) {
                Write-Warning "Rate limited (429) - waiting $retrySeconds seconds before retry $attempt/$maxRetries..."
                Start-Sleep -Seconds $retrySeconds
            } else {
                throw
            }
        }
    }
}

function Reorder-FilmEntry($film) {
    $ordered = [ordered]@{}
    foreach ($key in $fieldOrder) {
        if ($film.ContainsKey($key)) {
            $ordered[$key] = $film[$key]
        }
    }
    foreach ($key in @($film.Keys)) {
        if (-not $ordered.Contains($key)) {
            $ordered[$key] = $film[$key]
        }
    }
    return $ordered
}

# Read existing films database
$films = [System.Collections.Generic.List[object]]::new()
foreach ($doc in @(Import-Yaml -LiteralPath $DatabasePath)) {
    $films.Add($doc)
}

Write-Host "Read $($films.Count) entries from films database"

# --- Phase 1: Look up Wikidata QIDs for entries missing them ---

$needsQid = @{}
foreach ($film in $films) {
    if (-not $film.wikidata -and $film.providers -and $film.providers.netflix -and $null -ne $film.providers.netflix.id) {
        $needsQid[[string]$film.providers.netflix.id] = $film
    }
}

if ($needsQid.Count -gt 0) {
    Write-Host "$($needsQid.Count) entries need Wikidata QID lookup"

    $batchSize = 50
    $netflixIds = @($needsQid.Keys)
    $qidMatched = 0

    for ($i = 0; $i -lt $netflixIds.Count; $i += $batchSize) {
        $end = [Math]::Min($i + $batchSize - 1, $netflixIds.Count - 1)
        $batch = $netflixIds[$i..$end]
        $values = ($batch | ForEach-Object { "`"$_`"" }) -join " "
        $query = "SELECT ?item ?netflixId WHERE { VALUES ?netflixId { $values } ?item wdt:P1874 ?netflixId . }"

        try {
            foreach ($binding in (Invoke-WikidataSparql $query)) {
                $qid = $binding.item.value -replace '.*/entity/', ''
                $netflixId = $binding.netflixId.value
                if (-not $needsQid[$netflixId].Contains('wikidata')) {
                    $needsQid[$netflixId]['wikidata'] = $qid
                    $qidMatched++
                }
            }
        }
        catch {
            Write-Warning "Failed to query Wikidata QIDs for batch at index ${i}: $_"
        }

        Write-Host "QID lookup: $([Math]::Min($i + $batchSize, $netflixIds.Count))/$($netflixIds.Count)"

        if ($i + $batchSize -lt $netflixIds.Count) {
            Start-Sleep -Milliseconds 1000
        }
    }

    Write-Host "Matched $qidMatched/$($needsQid.Count) entries to Wikidata QIDs"
} else {
    Write-Host "All entries already have Wikidata QIDs"
}

# --- Phase 2: Populate additional fields from Wikidata ---

$needsData = @{}
foreach ($film in $films) {
    if ($film.wikidata -and -not $film.title) {
        $needsData[$film.wikidata] = $film
    }
}

if ($needsData.Count -gt 0) {
    Write-Host "$($needsData.Count) entries need Wikidata details"

    $batchSize = 50
    $qids = @($needsData.Keys)
    $enriched = 0

    for ($i = 0; $i -lt $qids.Count; $i += $batchSize) {
        $end = [Math]::Min($i + $batchSize - 1, $qids.Count - 1)
        $batch = $qids[$i..$end]
        $values = ($batch | ForEach-Object { "wd:$_" }) -join " "

        $query = @"
SELECT ?item
  (SAMPLE(?title_) AS ?title)
  (SAMPLE(?titleJa_) AS ?titleJa)
  (GROUP_CONCAT(DISTINCT ?typeLabel_; SEPARATOR="|") AS ?types)
  (SAMPLE(?originCountryLabel_) AS ?originCountry)
  (SAMPLE(?langCode_) AS ?originalLanguage)
  (SAMPLE(?startYear_) AS ?startYear)
  (SAMPLE(?pubYear_) AS ?pubYear)
  (SAMPLE(?imdbId_) AS ?imdbId)
  (SAMPLE(?malId_) AS ?malId)
  (SAMPLE(?tmdbMovieId_) AS ?tmdbMovieId)
  (SAMPLE(?tmdbTvId_) AS ?tmdbTvId)
WHERE {
  VALUES ?item { $values }
  OPTIONAL { ?item rdfs:label ?title_ . FILTER(LANG(?title_) = "en") }
  OPTIONAL { ?item rdfs:label ?titleJa_ . FILTER(LANG(?titleJa_) = "ja") }
  OPTIONAL { ?item wdt:P31 ?type_ . ?type_ rdfs:label ?typeLabel_ . FILTER(LANG(?typeLabel_) = "en") }
  OPTIONAL { ?item wdt:P495 ?originCountry_ . ?originCountry_ rdfs:label ?originCountryLabel_ . FILTER(LANG(?originCountryLabel_) = "en") }
  OPTIONAL { ?item wdt:P364 ?lang_ . ?lang_ wdt:P424 ?langCode_ }
  OPTIONAL { ?item wdt:P345 ?imdbId_ }
  OPTIONAL { ?item wdt:P4086 ?malId_ }
  OPTIONAL { ?item wdt:P4947 ?tmdbMovieId_ }
  OPTIONAL { ?item wdt:P4983 ?tmdbTvId_ }
  OPTIONAL { ?item wdt:P580 ?startTime_ . BIND(YEAR(?startTime_) AS ?startYear_) }
  OPTIONAL { ?item wdt:P577 ?pubDate_ . BIND(YEAR(?pubDate_) AS ?pubYear_) }
}
GROUP BY ?item
"@

        try {
            foreach ($binding in (Invoke-WikidataSparql $query)) {
                $qid = $binding.item.value -replace '.*/entity/', ''
                $film = $needsData[$qid]
                if (-not $film) { continue }

                if ($binding.title.value) {
                    $film['title'] = $binding.title.value
                }
                if ($binding.titleJa.value) {
                    $film['title-ja'] = $binding.titleJa.value
                }
                if ($binding.types.value) {
                    $types = $binding.types.value -split '\|' |
                        Where-Object { $_ -notin $excludedTypes } |
                        ForEach-Object { ($_.ToLower() -replace ' ', '-') }
                    if ($types) {
                        $film['type'] = ($types | Select-Object -First 1)
                    }
                }
                if ($binding.originCountry.value) {
                    $film['originCountry'] = $binding.originCountry.value
                }
                if ($binding.originalLanguage.value) {
                    $film['originalLanguage'] = $binding.originalLanguage.value
                }
                $year = if ($binding.startYear.value) { $binding.startYear.value } else { $binding.pubYear.value }
                if ($year) {
                    $film['year'] = [int]$year
                }
                if ($binding.imdbId.value) {
                    $film['imdb'] = @{ id = $binding.imdbId.value }
                }
                if ($binding.malId.value) {
                    $film['myAnimeList'] = @{ id = [int]$binding.malId.value }
                }
                $tmdbId = if ($binding.tmdbMovieId.value) { $binding.tmdbMovieId.value } else { $binding.tmdbTvId.value }
                if ($tmdbId) {
                    $tmdbKind = if ($binding.tmdbMovieId.value) { 'movie' } else { 'tv-series' }
                    $film['tmdb'] = [ordered]@{ id = [int]$tmdbId; kind = $tmdbKind }
                }
                $enriched++
            }
        }
        catch {
            Write-Warning "Failed to query Wikidata details for batch at index ${i}: $_"
        }

        Write-Host "Details lookup: $([Math]::Min($i + $batchSize, $qids.Count))/$($qids.Count)"

        if ($i + $batchSize -lt $qids.Count) {
            Start-Sleep -Milliseconds 1000
        }
    }

    Write-Host "Enriched $enriched/$($needsData.Count) entries with Wikidata details"
} else {
    Write-Host "All entries already have Wikidata details"
}

# Reorder keys and write films.yaml
for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Reorder-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done - $($films.Count) total entries in $DatabasePath"
