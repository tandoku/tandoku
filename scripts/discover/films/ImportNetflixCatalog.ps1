[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [string[]]$Country = @('US'),

    [string]$AudioLanguage = 'ja',

    [string]$SubtitleLanguage,

    [string]$CachePath,

    [int]$RequestLimit = 100,

    [string]$ApiKey = $env:RAPIDAPI_KEY
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"

if (-not $ApiKey) {
    throw 'A uNoGS RapidAPI key is required. Pass -ApiKey or set the RAPIDAPI_KEY environment variable.'
}

$script:ApiHost = 'unogsng.p.rapidapi.com'
$script:ApiHeaders = @{
    'x-rapidapi-key'  = $ApiKey
    'x-rapidapi-host' = $script:ApiHost
}

# Tracks uNoGS API usage so a run stays within -RequestLimit. Cached lookups do
# not count; only requests actually sent to the API are tallied.
$script:RequestCount = 0
$script:RequestLimit = $RequestLimit
$script:RequestLimitWarned = $false

# Single timestamp for the whole run, recorded on each Title Countries cache entry.
$script:RunTimestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')

# Returns $true while the API request budget has not been exhausted. Emits a
# single warning the first time the limit is reached so callers can skip the
# remaining work and resume on a later run (helped along by the cache).
function Test-RequestAllowed {
    if ($script:RequestLimit -gt 0 -and $script:RequestCount -ge $script:RequestLimit) {
        if (-not $script:RequestLimitWarned) {
            Write-Warning "Reached uNoGS request limit ($script:RequestLimit) - skipping further API requests. Re-run to continue."
            $script:RequestLimitWarned = $true
        }
        return $false
    }
    return $true
}

# Maps a lower-cased base language name (as returned by Netflix audio/subtitle
# lists, e.g. 'japanese', 'chinese') to its ISO 639 code. Built from .NET's
# neutral cultures so the common languages are covered without a hand-maintained
# table; the first code wins for names shared across cultures.
$script:LanguageCodeByName = @{}
foreach ($ci in [System.Globalization.CultureInfo]::GetCultures([System.Globalization.CultureTypes]::NeutralCultures)) {
    if ($ci.TwoLetterISOLanguageName) {
        $base = ($ci.EnglishName -split ' \(')[0].Trim().ToLowerInvariant()
        if ($base -and -not $script:LanguageCodeByName.ContainsKey($base)) {
            $script:LanguageCodeByName[$base] = $ci.TwoLetterISOLanguageName
        }
    }
}

# Netflix uses a few language names that don't match any .NET culture's English
# name; map them explicitly.
$languageAliases = @{
    'mandarin'  = 'zh'
    'cantonese' = 'zh'
    'flemish'   = 'nl'
    'farsi'     = 'fa'
    'tagalog'   = 'tl'
}
foreach ($alias in $languageAliases.Keys) {
    $script:LanguageCodeByName[$alias] = $languageAliases[$alias]
}

# Calls a uNoGS endpoint, URL-encoding query values and retrying on HTTP 429.
# Returns $null (without sending a request) once the -RequestLimit budget is
# spent.
function Invoke-UnogsApi {
    param(
        [Parameter(Mandatory)]
        [string]$Endpoint,

        [hashtable]$Query = @{}
    )

    if (-not (Test-RequestAllowed)) {
        return $null
    }
    $script:RequestCount++

    $pairs = foreach ($key in $Query.Keys) {
        $value = $Query[$key]
        if ($null -ne $value -and $value -ne '') {
            "$key=$([uri]::EscapeDataString([string]$value))"
        }
    }
    $url = "https://$($script:ApiHost)/$Endpoint"
    if ($pairs) {
        $url += '?' + ($pairs -join '&')
    }

    $maxRetries = 5
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        try {
            return Invoke-RestMethod -Uri $url -Headers $script:ApiHeaders -Method Get
        }
        catch {
            $status = $_.Exception.Response.StatusCode.value__
            if ($status -eq 429 -and $attempt -lt $maxRetries) {
                $retrySeconds = 2
                $retryAfter = $_.Exception.Response.Headers['Retry-After']
                if ($retryAfter -and [int]::TryParse($retryAfter, [ref]$null)) {
                    $retrySeconds = [int]$retryAfter
                }
                Write-Warning "Rate limited (429) - waiting $retrySeconds seconds before retry $attempt/$maxRetries..."
                Start-Sleep -Seconds $retrySeconds
            } else {
                throw
            }
        }
    }
}

# Converts a two-letter ISO 639-1 code into the Netflix language name used by the
# search endpoint's audio/subtitle filters (e.g. 'ja' -> 'japanese').
function Get-NetflixLanguageName([string]$code) {
    try {
        return [System.Globalization.CultureInfo]::GetCultureInfo($code).EnglishName.ToLowerInvariant()
    }
    catch {
        throw "Unknown language code '$code'."
    }
}

# Resolves a cleaned, lower-cased language name to an ISO 639 code. Netflix is
# inconsistent about region qualifiers (e.g. 'Chinese (Simplified)' vs
# 'Simplified Chinese'), so when a direct match fails we drop leading words and
# match on the trailing language name (e.g. 'simplified chinese' -> 'chinese').
function Resolve-LanguageCode([string]$name) {
    if ($script:LanguageCodeByName.ContainsKey($name)) {
        return $script:LanguageCodeByName[$name]
    }
    $words = $name -split '\s+'
    for ($i = 1; $i -lt $words.Count; $i++) {
        $candidate = ($words[$i..($words.Count - 1)] -join ' ')
        if ($script:LanguageCodeByName.ContainsKey($candidate)) {
            return $script:LanguageCodeByName[$candidate]
        }
    }
    return $null
}

# Parses a Netflix audio/subtitle list (e.g. 'English,Japanese [Original],
# Spanish (Latin America) - Audio Description') into a sorted, de-duplicated list
# of ISO 639 language codes.
function ConvertTo-LanguageCodes([string]$value) {
    $codes = [System.Collections.Generic.HashSet[string]]::new()
    if ($value) {
        foreach ($part in $value -split ',') {
            $name = $part -replace '\s*-\s*Audio Description\s*$', ''
            $name = $name -replace '\s*\[Original\]\s*$', ''
            $name = $name -replace '\s*\(.*\)\s*$', ''
            $name = $name.Trim().ToLowerInvariant()
            if (-not $name) { continue }

            $code = Resolve-LanguageCode $name
            if ($code) {
                [void]$codes.Add($code)
            } else {
                Write-Warning "Unrecognized Netflix language name '$name'"
            }
        }
    }
    return @($codes | Sort-Object)
}

# Builds the availability.netflix record, preserving any extra keys (e.g.
# watchlist) already present on the existing record.
function New-NetflixRecord($existing, $id, $title, $type, $year, $countryDetails) {
    $record = [ordered]@{
        id             = $id
        title          = $title
        type           = $type
        year           = $year
        countryDetails = $countryDetails
    }
    if ($existing) {
        foreach ($key in $existing.Keys) {
            if (-not $record.Contains($key)) {
                $record[$key] = $existing[$key]
            }
        }
    }
    return $record
}

# Cache state. countries.json stores the raw Countries response; titlecountries.json
# stores a netflix-id-keyed map of Title Countries responses (kept sorted by id
# for stable diffs). Both files are only used when -CachePath is supplied.
$script:CountriesCacheFile = $null
$script:TitleCountriesCacheFile = $null
$script:TitleCountriesCache = [ordered]@{}
if ($CachePath) {
    if (-not (Test-Path -LiteralPath $CachePath)) {
        New-Item -ItemType Directory -Path $CachePath -Force | Out-Null
    }
    $script:CountriesCacheFile = Join-Path $CachePath 'netflix-catalog-countries.json'
    $script:TitleCountriesCacheFile = Join-Path $CachePath 'netflix-catalog-titlecountries.json'
    if (Test-Path -LiteralPath $script:TitleCountriesCacheFile) {
        $loaded = Get-Content -LiteralPath $script:TitleCountriesCacheFile -Raw | ConvertFrom-Json -AsHashtable
        foreach ($key in ($loaded.Keys | Sort-Object { [long]$_ })) {
            $script:TitleCountriesCache[[string]$key] = $loaded[$key]
        }
    }
}

# Writes the titlecountries cache to disk, sorted by netflix id for stability.
function Save-TitleCountriesCache {
    if (-not $script:TitleCountriesCacheFile) {
        return
    }
    $sorted = [ordered]@{}
    foreach ($key in ($script:TitleCountriesCache.Keys | Sort-Object { [long]$_ })) {
        $sorted[$key] = $script:TitleCountriesCache[$key]
    }
    $sorted | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $script:TitleCountriesCacheFile
}

# Returns the Countries response, reading from / populating the cache when
# -CachePath is supplied.
function Get-CountriesData {
    if ($script:CountriesCacheFile -and (Test-Path -LiteralPath $script:CountriesCacheFile)) {
        return Get-Content -LiteralPath $script:CountriesCacheFile -Raw | ConvertFrom-Json
    }
    $result = Invoke-UnogsApi -Endpoint 'countries'
    if ($null -eq $result) {
        throw 'Unable to retrieve country list (uNoGS request limit reached). Increase -RequestLimit or cache the countries list.'
    }
    if ($script:CountriesCacheFile) {
        $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $script:CountriesCacheFile
    }
    return $result
}

# Returns the Title Countries results for a netflix id, reading from / populating
# the cache. Returns $null when the data is uncached and the request budget is
# spent.
function Get-TitleCountries([string]$netflixId) {
    if ($script:TitleCountriesCache.Contains($netflixId)) {
        return $script:TitleCountriesCache[$netflixId].results
    }
    $response = Invoke-UnogsApi -Endpoint 'titlecountries' -Query @{ netflixid = $netflixId }
    if ($null -eq $response) {
        return $null
    }
    $script:TitleCountriesCache[$netflixId] = [ordered]@{
        results   = $response.results
        timestamp = $script:RunTimestamp
    }
    Save-TitleCountriesCache
    return $response.results
}

# Resolve the requested country codes to uNoGS numeric country IDs.
$countriesResult = Get-CountriesData
$countryIdByCode = @{}
foreach ($entry in $countriesResult.results) {
    $countryIdByCode[$entry.countrycode.ToUpperInvariant()] = $entry.id
}

$countryCodes = @($Country | ForEach-Object { $_.ToUpperInvariant() })
$countryIds = foreach ($code in $countryCodes) {
    if (-not $countryIdByCode.ContainsKey($code)) {
        throw "Unknown country code '$code'. Expected an ISO 3166-1 alpha-2 code available on Netflix (e.g. US, JP)."
    }
    $countryIdByCode[$code]
}

# Build the search filter.
$searchQuery = [ordered]@{
    countrylist         = ($countryIds -join ',')
    country_andorunique = 'or'
    orderby             = 'date'
    limit               = 100
    offset              = 0
}
if ($AudioLanguage) {
    $searchQuery['audio'] = Get-NetflixLanguageName $AudioLanguage
}
if ($SubtitleLanguage) {
    $searchQuery['subtitle'] = Get-NetflixLanguageName $SubtitleLanguage
}
if ($AudioLanguage -and $SubtitleLanguage) {
    $searchQuery['audiosubtitle_andor'] = 'and'
}

$filterParts = @()
if ($AudioLanguage) { $filterParts += "audio=$AudioLanguage" }
if ($SubtitleLanguage) { $filterParts += "subtitle=$SubtitleLanguage" }
$filterDesc = if ($filterParts) { $filterParts -join ', ' } else { '(no language filter)' }
Write-Host "Searching Netflix catalog in $($countryCodes -join ', ') with $filterDesc"

# Page through the search results.
$searchResults = [System.Collections.Generic.List[object]]::new()
$total = $null
while ($true) {
    $response = Invoke-UnogsApi -Endpoint 'search' -Query $searchQuery
    if ($null -eq $response) {
        break
    }
    if ($null -eq $total) {
        $total = $response.total
        Write-Host "Found $total matching titles"
    }
    foreach ($result in $response.results) {
        $searchResults.Add($result)
    }
    $searchQuery['offset'] = [int]$searchQuery['offset'] + [int]$searchQuery['limit']
    if ([int]$searchQuery['offset'] -ge [int]$total) {
        break
    }
}

# Read existing films database.
$films = Read-FilmsDatabase -LiteralPath $DatabasePath -AllowMissing
Write-Host "Read $($films.Count) existing entries from films database"

# Build lookup table for existing films by Netflix ID.
$filmsByNetflixId = @{}
for ($i = 0; $i -lt $films.Count; $i++) {
    $film = $films[$i]
    if ($film.availability -and $film.availability.netflix -and $null -ne $film.availability.netflix.id) {
        $filmsByNetflixId[[string]$film.availability.netflix.id] = $i
    }
}

# Process each matching title.
$added = 0
$updated = 0
$skipped = 0
$processed = 0
foreach ($result in $searchResults) {
    $netflixId = [string]$result.nfid
    $processed++
    Write-Host "[$processed/$($searchResults.Count)] $($result.title) ($netflixId)"

    # Retrieve per-country availability details (cached when possible).
    $titleCountries = Get-TitleCountries $netflixId
    if ($null -eq $titleCountries) {
        # Request budget spent and no cached details - leave this title for a
        # later run.
        $skipped++
        continue
    }
    $countryDetails = [ordered]@{}
    foreach ($code in $countryCodes) {
        $detail = $titleCountries | Where-Object { $_.cc -eq $code } | Select-Object -First 1
        if ($detail) {
            $countryDetail = [ordered]@{}
            if ($null -ne $detail.seasdet -and $detail.seasdet -ne '') {
                $countryDetail['seasonDetails'] = $detail.seasdet
            }
            $countryDetail['newDate'] = $detail.newdate
            if ($null -ne $detail.expiredate -and $detail.expiredate -ne '') {
                $countryDetail['expireDate'] = $detail.expiredate
            }
            $countryDetail['audio'] = ConvertTo-LanguageCodes $detail.audio
            $countryDetail['subtitle'] = ConvertTo-LanguageCodes $detail.subtitle
            $countryDetails[$code] = $countryDetail
        }
    }

    if ($filmsByNetflixId.ContainsKey($netflixId)) {
        # Update existing entry, preserving other fields (e.g. watchlist).
        $film = $films[$filmsByNetflixId[$netflixId]]
        $film['availability']['netflix'] = New-NetflixRecord `
            $film['availability']['netflix'] ([int]$result.nfid) $result.title $result.vtype $result.year $countryDetails
        Add-Origin $film 'netflix'
        $updated++
    } else {
        # Add new entry.
        $newFilm = [ordered]@{
            availability = [ordered]@{
                netflix = New-NetflixRecord $null ([int]$result.nfid) $result.title $result.vtype $result.year $countryDetails
            }
        }
        Add-Origin $newFilm 'netflix'
        $films.Add($newFilm)
        $filmsByNetflixId[$netflixId] = $films.Count - 1
        $added++
    }
}

# Write films.yaml
$films | Export-Yaml -Path $DatabasePath

Write-Host "Done: $added added, $updated updated, $skipped skipped - $($films.Count) total entries ($($script:RequestCount) API requests)"
