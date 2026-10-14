[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [string]$Language = 'ja',

    [switch]$Force,

    [string]$LogPath
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-catalog-films.psm1"
Import-Module "$PSScriptRoot/../../modules/tandoku-log.psm1"

# When -LogPath is supplied, additionally record warnings and errors (including
# uncaught terminating errors) to that file. See tandoku-log.psm1.
Initialize-TandokuLog -LogPath $LogPath
trap { Write-TandokuLogEntry 'ERROR' $_; break }

function Clean-Text($text) {
    if ($null -eq $text) { return $text }
    # Strip BOM / zero-width characters that occasionally appear in Wikidata labels
    return ($text -replace '[\uFEFF\u200B\u200C\u200D]', '').Trim()
}

# Read existing films database
$films = Read-FilmsDatabase -LiteralPath $DatabasePath

Write-Host "Read $($films.Count) entries from films database"

# A record's origins drive Wikidata QID lookup; records that have lost all of
# their origins (e.g. an IMDb-list-only film pruned out of every list) are not
# matched to Wikidata by identifier, though existing details are still refreshed
# when they already have an identifiers.wikidata.id.
function Test-FilmHasOrigin($film) {
    return ($film.Contains('origin') -and $film['origin'] -and @($film['origin']).Count -gt 0)
}

$noOriginCount = @($films | Where-Object { -not (Test-FilmHasOrigin $_) }).Count
if ($noOriginCount -gt 0) {
    Write-Warning "$noOriginCount film record(s) have no origin and will not be looked up by identifier; run PruneFilms.ps1 to remove these stale records."
}

# Batched lookup of Wikidata QIDs by an external-identifier property (e.g.
# P1874 = Netflix ID, P345 = IMDb ID). Returns a hashtable mapping each id to a
# list of matching QIDs (ids with no match are absent).
function Resolve-WikidataQidsByProperty([string]$property, [string[]]$ids, [string]$label = $property) {
    $result = @{}
    $batchSize = 50
    for ($i = 0; $i -lt $ids.Count; $i += $batchSize) {
        $end = [Math]::Min($i + $batchSize - 1, $ids.Count - 1)
        $batch = $ids[$i..$end]
        $values = ($batch | ForEach-Object { "`"$_`"" }) -join " "
        $query = "SELECT ?item ?id WHERE { VALUES ?id { $values } ?item wdt:$property ?id . }"

        try {
            foreach ($binding in (Invoke-WikidataSparql $query)) {
                $qid = ConvertTo-WikidataQid $binding.item.value
                $id = $binding.id.value
                if (-not $result.ContainsKey($id)) {
                    $result[$id] = [System.Collections.Generic.List[string]]::new()
                }
                if ($result[$id] -notcontains $qid) {
                    $result[$id].Add($qid)
                }
            }
        }
        catch {
            Write-Warning "Failed to query Wikidata QIDs for $label batch at index ${i}: $_"
        }

        Write-Host "QID lookup ($label): $([Math]::Min($i + $batchSize, $ids.Count))/$($ids.Count)"

        if ($i + $batchSize -lt $ids.Count) {
            Start-Sleep -Milliseconds 1000
        }
    }
    return $result
}

# --- Phase 1: Look up Wikidata QIDs for entries missing them ---

# Resolve from identifiers backed by import origins, requiring agreement.
$needsQid = [System.Collections.Generic.List[object]]::new()
foreach ($film in $films) {
    if (-not ($Force -or -not (Get-FilmIdentifier $film 'wikidata'))) { continue }
    $owned = Get-FilmOwnedIdentifiers $film
    if ($owned.Count -gt 0) {
        $needsQid.Add([pscustomobject]@{ Film = $film; Owned = $owned })
    }
}

if ($needsQid.Count -gt 0) {
    Write-Host "$($needsQid.Count) entries need Wikidata QID lookup"

    # Collect the distinct owned identifiers per Wikidata property.
    $idsByProperty = @{}
    foreach ($req in $needsQid) {
        foreach ($owned in $req.Owned) {
            if (-not $idsByProperty.ContainsKey($owned.WikidataProp)) {
                $idsByProperty[$owned.WikidataProp] = [System.Collections.Generic.HashSet[string]]::new()
            }
            [void]$idsByProperty[$owned.WikidataProp].Add([string]$owned.Id)
        }
    }

    # Resolve QIDs for each property and index by "property|id".
    $originByProperty = @{}
    foreach ($entry in (Get-FilmOriginRegistry)) {
        $originByProperty[$entry.WikidataProp] = $entry.Identifier
    }
    $qidByPropertyId = @{}
    foreach ($property in $idsByProperty.Keys) {
        $propertyName = if ($originByProperty.ContainsKey($property)) { $originByProperty[$property] } else { $property }
        $map = Resolve-WikidataQidsByProperty $property @($idsByProperty[$property]) $propertyName
        foreach ($id in $map.Keys) {
            $qids = $map[$id]
            if ($qids.Count -gt 1) {
                Write-Warning "$propertyName identifier $id matches multiple Wikidata entities: $($qids -join ', ')"
            }
            $qidByPropertyId["$property|$id"] = $qids
        }
    }

    # Assign a QID per record, requiring all of its owned identifiers to agree.
    $qidMatched = 0
    foreach ($req in $needsQid) {
        $film = $req.Film
        $resolved = [System.Collections.Generic.HashSet[string]]::new()
        foreach ($owned in $req.Owned) {
            $key = "$($owned.WikidataProp)|$($owned.Id)"
            if ($qidByPropertyId.ContainsKey($key)) {
                foreach ($qid in $qidByPropertyId[$key]) { [void]$resolved.Add($qid) }
            }
        }

        if ($resolved.Count -eq 0) { continue }

        if ($resolved.Count -gt 1) {
            $idDesc = ($req.Owned | ForEach-Object { "$($_.Origin)=$($_.Id)" }) -join ', '
            Write-Error "Film with identifiers [$idDesc] resolves to multiple Wikidata entities: $(@($resolved) -join ', '). Leaving identifiers.wikidata unchanged."
            continue
        }

        if ($Force -or -not (Get-FilmIdentifier $film 'wikidata')) {
            Set-FilmIdentifier $film 'wikidata' @($resolved)[0]
            $qidMatched++
        }
    }

    Write-Host "Matched $qidMatched/$($needsQid.Count) entries to Wikidata QIDs"
} else {
    Write-Host "All entries already have Wikidata QIDs"
}

# --- Phase 1.5: Merge duplicate records that resolve to the same entity ---

# Different origins can create separate records for the same work (e.g. a Netflix
# import and an IMDb list import); once both resolve to the same QID they must be
# merged into a single record.
$byQid = @{}
for ($i = 0; $i -lt $films.Count; $i++) {
    $film = $films[$i]
    $qid = [string](Get-FilmIdentifier $film 'wikidata')
    if ($qid) {
        if (-not $byQid.ContainsKey($qid)) {
            $byQid[$qid] = [System.Collections.Generic.List[int]]::new()
        }
        $byQid[$qid].Add($i)
    }
}

$mergedAway = [System.Collections.Generic.HashSet[int]]::new()
$mergeCount = 0
foreach ($qid in $byQid.Keys) {
    $indices = $byQid[$qid]
    if ($indices.Count -lt 2) { continue }

    $primary = $films[$indices[0]]
    for ($j = 1; $j -lt $indices.Count; $j++) {
        $secondaryIdx = $indices[$j]
        if (Merge-FilmRecords $primary $films[$secondaryIdx]) {
            [void]$mergedAway.Add($secondaryIdx)
            $mergeCount++
        }
    }
}

if ($mergedAway.Count -gt 0) {
    $merged = [System.Collections.Generic.List[object]]::new()
    for ($i = 0; $i -lt $films.Count; $i++) {
        if (-not $mergedAway.Contains($i)) {
            $merged.Add($films[$i])
        }
    }
    $films = $merged
    Write-Host "Merged $mergeCount duplicate record(s); $($films.Count) entries remain"
}

# --- Phase 2: Populate additional fields from Wikidata ---

$needsData = @{}
foreach ($film in $films) {
    $qid = [string](Get-FilmIdentifier $film 'wikidata')
    $wikidata = Get-FilmSource $film 'wikidata'
    if ($qid -and ($Force -or -not $wikidata.title)) {
        if (-not $needsData.ContainsKey($qid)) {
            $needsData[$qid] = [System.Collections.Generic.List[object]]::new()
        }
        $needsData[$qid].Add($film)
    }
}

if ($needsData.Count -gt 0) {
    $needsDataFilmCount = ($needsData.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
    Write-Host "$needsDataFilmCount entries need Wikidata details"

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
  (GROUP_CONCAT(DISTINCT ?netflixId_; SEPARATOR="|") AS ?netflixId)
  (GROUP_CONCAT(DISTINCT ?malIdOrd_; SEPARATOR="|") AS ?malId)
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
  OPTIONAL { ?item wdt:P1874 ?netflixId_ }
  OPTIONAL {
    ?item p:P4086 ?malStmt_ .
    ?malStmt_ ps:P4086 ?malIdVal_ .
    FILTER NOT EXISTS { ?malStmt_ wikibase:rank wikibase:DeprecatedRank }
    OPTIONAL { ?malStmt_ pq:P1545 ?malOrd_ }
    BIND(EXISTS { ?malStmt_ pq:P1810 ?malNamedAs_ } AS ?malHasNamedAs_)
    BIND(CONCAT(?malIdVal_, "~", IF(BOUND(?malOrd_), STR(?malOrd_), ""), "~", IF(?malHasNamedAs_, "1", "0")) AS ?malIdOrd_)
  }
  OPTIONAL { ?item wdt:P4947 ?tmdbMovieId_ }
  OPTIONAL { ?item wdt:P4983 ?tmdbTvId_ }
  OPTIONAL { ?item wdt:P580 ?startTime_ . BIND(YEAR(?startTime_) AS ?startYear_) }
  OPTIONAL { ?item wdt:P577 ?pubDate_ . BIND(YEAR(?pubDate_) AS ?pubYear_) }
}
GROUP BY ?item
"@

        try {
            foreach ($binding in (Invoke-WikidataSparql $query)) {
                $qid = ConvertTo-WikidataQid $binding.item.value
                if (-not $needsData.ContainsKey($qid)) { continue }

                # --- Parse the Wikidata values once for this entity ---

                $title = [ordered]@{}
                if ($binding.titleEn.value) {
                    $title['en'] = Clean-Text $binding.titleEn.value
                }
                if ($Language -ne 'en' -and $binding.titleLang.value) {
                    $title[$Language] = Clean-Text $binding.titleLang.value
                }
                $type = $null
                if ($binding.types.value) {
                    $types = @($binding.types.value -split '\|' |
                        ForEach-Object { ($_.ToLower() -replace ' ', '-') } |
                        Sort-Object)
                    if ($types.Count -gt 0) {
                        $type = $types
                    }
                }
                $country = $null
                if ($binding.country.value) {
                    $country = @($binding.country.value -split '\|' | Sort-Object)
                }
                # Prefer P364 (original language of film or TV show); fall back to
                # P407 (language of work or name) when P364 is not set.
                $langValue = if ($binding.language.value) {
                    $binding.language.value
                } else {
                    $binding.fallbackLanguage.value
                }
                $languageCodes = $null
                if ($langValue) {
                    $languageCodes = @($langValue -split '\|' | Sort-Object)
                }
                $year = if ($binding.startYear.value) { $binding.startYear.value } else { $binding.pubYear.value }

                $imdbIds = @()
                if ($binding.imdbId.value) {
                    $imdbIds = @($binding.imdbId.value -split '\|' | Sort-Object)
                }
                $netflixIds = @()
                if ($binding.netflixId.value) {
                    $netflixIds = @($binding.netflixId.value -split '\|')
                }
                $malIds = @()
                $malEntries = @()
                if ($binding.malId.value) {
                    $malEntries = @($binding.malId.value -split '\|' | ForEach-Object {
                        $idPart, $ordPart, $namedPart = $_ -split '~', 3
                        [pscustomobject]@{
                            Id = [int]$idPart
                            Ordinal = if ($null -ne $ordPart -and $ordPart -ne '') { [int]$ordPart } else { $null }
                            HasNamedAs = ($namedPart -eq '1')
                        }
                    })
                    $malIds = @($malEntries | ForEach-Object { $_.Id } | Sort-Object)
                }
                $tmdbMovieIds = @()
                if ($binding.tmdbMovieId.value) {
                    $tmdbMovieIds = @($binding.tmdbMovieId.value -split '\|' | ForEach-Object { [int]$_ } | Sort-Object)
                }
                $tmdbTvIds = @()
                if ($binding.tmdbTvId.value) {
                    $tmdbTvIds = @($binding.tmdbTvId.value -split '\|' | ForEach-Object { [int]$_ } | Sort-Object)
                }

                # --- Apply to every record resolved to this entity ---

                foreach ($film in $needsData[$qid]) {
                    $origins = @()
                    if ($film.Contains('origin') -and $film['origin']) {
                        $origins = @($film['origin'])
                    }
                    $wikidata = Get-FilmSource $film 'wikidata' -Create

                    if ($title.Count -gt 0) {
                        $wikidata['title'] = $title
                    } elseif ($Force) {
                        $wikidata.Remove('title')
                    }
                    if ($type) {
                        $wikidata['type'] = $type
                    } elseif ($Force) {
                        $wikidata.Remove('type')
                    }
                    if ($country) {
                        $wikidata['country'] = $country
                    } elseif ($Force) {
                        $wikidata.Remove('country')
                    }
                    if ($languageCodes) {
                        $wikidata['language'] = $languageCodes
                    } elseif ($Force) {
                        $wikidata.Remove('language')
                    }
                    if ($year) {
                        $wikidata['year'] = [int]$year
                    } elseif ($Force) {
                        $wikidata.Remove('year')
                    }

                    # IMDb ID is backed by the 'imdb-lists' origin: when owned it is
                    # authoritative and must never be overwritten from Wikidata -
                    # a mismatch is an error. When not owned (e.g. a Netflix-only
                    # record) the IMDb ID is a cross-reference we fill in.
                    if ($origins -contains 'imdb-lists') {
                        $currentImdbId = Get-FilmIdentifier $film 'imdb'
                        if ($imdbIds.Count -gt 0 -and $currentImdbId -and ($imdbIds -notcontains $currentImdbId)) {
                            Write-Error "${qid}: Wikidata IMDb ID(s) [$($imdbIds -join ', ')] do not match imported identifiers.imdb.id '$currentImdbId'; not overwriting."
                        }
                    } elseif ($imdbIds.Count -gt 0) {
                        if ($imdbIds.Count -gt 1) {
                            Write-Warning "$qid has multiple IMDb IDs: $($imdbIds -join ', '); using $($imdbIds[0])"
                        }
                        Set-FilmIdentifier $film 'imdb' $imdbIds[0]
                    } elseif ($Force) {
                        $film['identifiers'].Remove('imdb')
                    }

                    # Either Netflix import origin protects the shared ID, never
                    # populated from Wikidata; verify consistency and error on a
                    # mismatch.
                    if (($origins -contains 'netflix-unogs' -or $origins -contains 'netflix-watchlist') -and $netflixIds.Count -gt 0) {
                        $currentNetflixId = Get-FilmIdentifier $film 'netflix'
                        if ($currentNetflixId -and ($netflixIds -notcontains "$currentNetflixId")) {
                            Write-Error "${qid}: Wikidata Netflix ID(s) [$($netflixIds -join ', ')] do not match imported identifiers.netflix.id '$currentNetflixId'; not overwriting."
                        }
                    }

                    if ($malEntries.Count -gt 0) {
                        if ($malEntries.Count -gt 1) {
                            # When multiple IDs are present, ignore any tagged with a
                            # 'subject named as' (P1810) qualifier, which typically marks
                            # alternate/related entries. If every ID is so tagged, the
                            # entry is genuinely ambiguous, so warn with all IDs.
                            $candidates = @($malEntries | Where-Object { -not $_.HasNamedAs })
                            if ($candidates.Count -eq 0) {
                                Write-Warning "$qid has multiple MyAnimeList IDs: $($malIds -join ', '); using $($malIds[0])"
                                Set-FilmIdentifier $film 'myAnimeList' $malIds[0]
                            } else {
                                $candidateIds = @($candidates | ForEach-Object { $_.Id } | Sort-Object)
                                # Multiple candidates are valid when disambiguated by a
                                # 'series ordinal' (P1545); use the lowest ordinal without
                                # warning.
                                $allHaveOrdinal = @($candidates | Where-Object { $null -eq $_.Ordinal }).Count -eq 0
                                if ($candidates.Count -gt 1 -and $allHaveOrdinal) {
                                    $selectedMalId = ($candidates | Sort-Object Ordinal, Id | Select-Object -First 1).Id
                                } else {
                                    if ($candidates.Count -gt 1) {
                                        Write-Warning "$qid has multiple MyAnimeList IDs: $($candidateIds -join ', '); using $($candidateIds[0])"
                                    }
                                    $selectedMalId = $candidateIds[0]
                                }
                                Set-FilmIdentifier $film 'myAnimeList' $selectedMalId
                            }
                        } else {
                            Set-FilmIdentifier $film 'myAnimeList' $malIds[0]
                        }
                    } elseif ($Force) {
                        $film['identifiers'].Remove('myAnimeList')
                    }

                    if ($tmdbMovieIds.Count + $tmdbTvIds.Count -gt 1) {
                        $tmdbDesc = @()
                        if ($tmdbMovieIds.Count -gt 0) { $tmdbDesc += "movie: $($tmdbMovieIds -join ', ')" }
                        if ($tmdbTvIds.Count -gt 0) { $tmdbDesc += "tv-series: $($tmdbTvIds -join ', ')" }
                        Write-Warning "$qid has multiple TMDB IDs ($($tmdbDesc -join '; ')); preferring tv-series over movie and lowest ID"
                    }
                    if ($tmdbTvIds.Count -gt 0) {
                        Set-FilmIdentifier $film 'tmdb' $tmdbTvIds[0] 'tv-series'
                    } elseif ($tmdbMovieIds.Count -gt 0) {
                        Set-FilmIdentifier $film 'tmdb' $tmdbMovieIds[0] 'movie'
                    } elseif ($Force) {
                        $film['identifiers'].Remove('tmdb')
                    }
                    $enriched++
                }
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

    Write-Host "Enriched $enriched/$needsDataFilmCount entries with Wikidata details"
} else {
    Write-Host "All entries already have Wikidata details"
}

# Reorder keys and write film-catalog.yaml
for ($i = 0; $i -lt $films.Count; $i++) {
    $films[$i] = Format-FilmEntry $films[$i]
}
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done - $($films.Count) total entries in $DatabasePath"


