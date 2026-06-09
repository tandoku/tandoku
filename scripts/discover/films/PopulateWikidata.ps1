[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [string]$Language = 'ja',

    [switch]$Force
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"

$sparqlHeaders = @{ "User-Agent" = "tandoku-discover/1.0 (https://github.com/tandoku)" }

# Preferred key order for film entries
$fieldOrder = @('wikidata', 'title', 'type', 'country', 'language', 'year', 'imdb', 'myAnimeList', 'tmdb', 'availability')

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
    if ($film.availability -and $film.availability.netflix -and $null -ne $film.availability.netflix.id) {
        if ($Force -or -not $film.wikidata) {
            $needsQid[[string]$film.availability.netflix.id] = $film
        }
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
            $batchResults = @{}
            foreach ($binding in (Invoke-WikidataSparql $query)) {
                $qid = $binding.item.value -replace '.*/entity/', ''
                $netflixId = $binding.netflixId.value
                if (-not $batchResults.ContainsKey($netflixId)) {
                    $batchResults[$netflixId] = [System.Collections.Generic.List[string]]::new()
                }
                $batchResults[$netflixId].Add($qid)
            }
            foreach ($netflixId in $batchResults.Keys) {
                $qids = $batchResults[$netflixId]
                if ($qids.Count -gt 1) {
                    Write-Warning "Netflix ID $netflixId matches multiple Wikidata entities: $($qids -join ', ')"
                }
                if ($Force -or -not $needsQid[$netflixId].Contains('wikidata')) {
                    $needsQid[$netflixId]['wikidata'] = $qids[0]
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
    if ($film.wikidata -and ($Force -or -not $film.title)) {
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
  (SAMPLE(?titleEn_) AS ?titleEn)
  (SAMPLE(?titleLang_) AS ?titleLang)
  (GROUP_CONCAT(DISTINCT ?typeLabel_; SEPARATOR="|") AS ?types)
  (GROUP_CONCAT(DISTINCT ?countryLabel_; SEPARATOR="|") AS ?country)
  (GROUP_CONCAT(DISTINCT ?langCode_; SEPARATOR="|") AS ?language)
  (GROUP_CONCAT(DISTINCT ?fallbackLangCode_; SEPARATOR="|") AS ?fallbackLanguage)
  (SAMPLE(?startYear_) AS ?startYear)
  (SAMPLE(?pubYear_) AS ?pubYear)
  (GROUP_CONCAT(DISTINCT ?imdbId_; SEPARATOR="|") AS ?imdbId)
  (GROUP_CONCAT(DISTINCT ?malId_; SEPARATOR="|") AS ?malId)
  (GROUP_CONCAT(DISTINCT ?tmdbMovieId_; SEPARATOR="|") AS ?tmdbMovieId)
  (GROUP_CONCAT(DISTINCT ?tmdbTvId_; SEPARATOR="|") AS ?tmdbTvId)
WHERE {
  VALUES ?item { $values }
  OPTIONAL { ?item rdfs:label ?titleEn_ . FILTER(LANG(?titleEn_) = "en") }
  OPTIONAL { ?item rdfs:label ?titleLang_ . FILTER(LANG(?titleLang_) = "$Language") }
  OPTIONAL { ?item wdt:P31 ?type_ . ?type_ rdfs:label ?typeLabel_ . FILTER(LANG(?typeLabel_) = "en") }
  OPTIONAL { ?item wdt:P495 ?country_ . ?country_ rdfs:label ?countryLabel_ . FILTER(LANG(?countryLabel_) = "en") }
  OPTIONAL { ?item wdt:P364 ?lang_ . ?lang_ wdt:P424 ?langCode_ }
  OPTIONAL { ?item wdt:P407 ?fallbackLang_ . ?fallbackLang_ wdt:P424 ?fallbackLangCode_ }
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

                $title = [ordered]@{}
                if ($binding.titleEn.value) {
                    $title['en'] = $binding.titleEn.value
                }
                if ($Language -ne 'en' -and $binding.titleLang.value) {
                    $title[$Language] = $binding.titleLang.value
                }
                if ($title.Count -gt 0) {
                    $film['title'] = $title
                } elseif ($Force) {
                    $film.Remove('title')
                }
                $type = $null
                if ($binding.types.value) {
                    $types = @($binding.types.value -split '\|' |
                        ForEach-Object { ($_.ToLower() -replace ' ', '-') })
                    if ($types.Count -gt 0) {
                        $type = $types
                    }
                }
                if ($type) {
                    $film['type'] = $type
                } elseif ($Force) {
                    $film.Remove('type')
                }
                if ($binding.country.value) {
                    $film['country'] = @($binding.country.value -split '\|')
                } elseif ($Force) {
                    $film.Remove('country')
                }
                # Prefer P364 (original language of film or TV show); fall back to
                # P407 (language of work or name) when P364 is not set.
                $language = if ($binding.language.value) {
                    $binding.language.value
                } else {
                    $binding.fallbackLanguage.value
                }
                if ($language) {
                    $film['language'] = @($language -split '\|')
                } elseif ($Force) {
                    $film.Remove('language')
                }
                $year = if ($binding.startYear.value) { $binding.startYear.value } else { $binding.pubYear.value }
                if ($year) {
                    $film['year'] = [int]$year
                } elseif ($Force) {
                    $film.Remove('year')
                }
                if ($binding.imdbId.value) {
                    $imdbIds = @($binding.imdbId.value -split '\|' | Sort-Object)
                    if ($imdbIds.Count -gt 1) {
                        Write-Warning "$qid has multiple IMDb IDs: $($imdbIds -join ', '); using $($imdbIds[0])"
                    }
                    $film['imdb'] = @{ id = $imdbIds[0] }
                } elseif ($Force) {
                    $film.Remove('imdb')
                }
                if ($binding.malId.value) {
                    $malIds = @($binding.malId.value -split '\|' | ForEach-Object { [int]$_ } | Sort-Object)
                    if ($malIds.Count -gt 1) {
                        Write-Warning "$qid has multiple MyAnimeList IDs: $($malIds -join ', '); using $($malIds[0])"
                    }
                    $film['myAnimeList'] = @{ id = $malIds[0] }
                } elseif ($Force) {
                    $film.Remove('myAnimeList')
                }
                $tmdbMovieIds = @()
                if ($binding.tmdbMovieId.value) {
                    $tmdbMovieIds = @($binding.tmdbMovieId.value -split '\|' | ForEach-Object { [int]$_ } | Sort-Object)
                }
                $tmdbTvIds = @()
                if ($binding.tmdbTvId.value) {
                    $tmdbTvIds = @($binding.tmdbTvId.value -split '\|' | ForEach-Object { [int]$_ } | Sort-Object)
                }
                if ($tmdbMovieIds.Count + $tmdbTvIds.Count -gt 1) {
                    $tmdbDesc = @()
                    if ($tmdbMovieIds.Count -gt 0) { $tmdbDesc += "movie: $($tmdbMovieIds -join ', ')" }
                    if ($tmdbTvIds.Count -gt 0) { $tmdbDesc += "tv-series: $($tmdbTvIds -join ', ')" }
                    Write-Warning "$qid has multiple TMDB IDs ($($tmdbDesc -join '; ')); preferring tv-series over movie and lowest ID"
                }
                if ($tmdbTvIds.Count -gt 0) {
                    $film['tmdb'] = [ordered]@{ id = $tmdbTvIds[0]; kind = 'tv-series' }
                } elseif ($tmdbMovieIds.Count -gt 0) {
                    $film['tmdb'] = [ordered]@{ id = $tmdbMovieIds[0]; kind = 'movie' }
                } elseif ($Force) {
                    $film.Remove('tmdb')
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
