[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$scriptsPath = Split-Path $PSScriptRoot -Parent
Import-Module "$scriptsPath/tandoku-discover-films.psm1"
Import-Module "$scriptsPath/../../modules/tandoku-yaml.psm1"

$testPath = Join-Path ([System.IO.Path]::GetTempPath()) "tandoku-film-tests-$([guid]::NewGuid())"
$null = New-Item -ItemType Directory -Path $testPath
$assertions = 0

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "Assertion failed: $message" }
    $script:assertions++
}

function Assert-Equal($actual, $expected, [string]$message) {
    $a = ConvertTo-Json -InputObject $actual -Depth 100 -Compress
    $b = ConvertTo-Json -InputObject $expected -Depth 100 -Compress
    Assert-True ($a -ceq $b) "$message (actual: $a, expected: $b)"
}

function Write-Json($value, [string]$path) {
    ConvertTo-Json -InputObject $value -Depth 100 | Set-Content -LiteralPath $path
}

function New-Film($identifiers, $sources, $origins) {
    return [ordered]@{ origin = @($origins); identifiers = $identifiers; sources = $sources }
}

$previousHttpFunction = Get-Item Function:global:Invoke-RestMethod -ErrorAction SilentlyContinue
$global:FilmTestRequests = [System.Collections.Generic.List[string]]::new()
$global:FilmTestHttpMode = 'blocked'
$global:FilmTestBindings = @()
$global:FilmTestQidBindings = @()
$global:FilmTestApiKeys = [System.Collections.Generic.List[string]]::new()
$global:FilmTestConfig = @{}
$global:FilmTestConfigKeys = [System.Collections.Generic.List[string]]::new()
function global:Invoke-RestMethod {
    param([string]$Uri, $Headers, $Method, $Body)
    $global:FilmTestRequests.Add($Uri)
    if ($global:FilmTestHttpMode -eq 'catalog' -and $Uri -like 'https://unogsng.p.rapidapi.com/search?*') {
        $global:FilmTestApiKeys.Add($Headers['x-rapidapi-key'])
        return @{
            total = 2
            results = @(
                @{ nfid = 100; title = 'Catalog Title'; vtype = 'series'; year = '2022' },
                @{ nfid = 101; title = 'Uncached Title'; vtype = 'movie'; year = '2023' }
            )
        }
    }
    if ($global:FilmTestHttpMode -eq 'tmdb' -and $Uri -like 'https://api.themoviedb.org/3/tv/20?*') {
        return @{ id = 20; poster_path = '/poster.jpg' }
    }
    if ($global:FilmTestHttpMode -eq 'wikidata' -and $Uri -like 'https://query.wikidata.org/sparql?*') {
        $query = [uri]::UnescapeDataString($Uri)
        $bindings = if ($query -like '*GROUP BY ?item*') { $global:FilmTestBindings } else { $global:FilmTestQidBindings }
        return @{ results = @{ bindings = $bindings } }
    }
    throw "Unexpected network request (no real HTTP is allowed in these tests): $Uri"
}

try {
    $identifierKeys = @('imdb', 'myAnimeList', 'netflix', 'tmdb', 'wikidata')
    $sourceKeys = @('imdb', 'imdb-lists', 'natively', 'netflix-unogs', 'netflix-watchlist', 'tmdb', 'wikidata')
    $identifiers = [ordered]@{}
    $sources = [ordered]@{}
    foreach ($name in @('wikidata', 'tmdb', 'netflix', 'myAnimeList', 'imdb')) {
        $identifiers[$name] = [ordered]@{ kind = 'movie'; id = "id-$name" }
    }
    foreach ($name in @('wikidata', 'tmdb', 'netflix-watchlist', 'netflix-unogs', 'natively', 'imdb-lists', 'imdb')) {
        $sources[$name] = [ordered]@{ year = 2022; title = $name; language = @('ja') }
    }
    $unsorted = [ordered]@{ sources = $sources; custom = 'retained'; identifiers = $identifiers; origin = @('imdb-lists') }
    $formatted = Format-FilmEntry $unsorted
    Assert-Equal @($formatted.Keys) @('origin', 'identifiers', 'sources', 'custom') 'Formatting preserves canonical top-level order'
    Assert-Equal @($formatted.identifiers.Keys) $identifierKeys 'Identifier keys are alphabetically sorted'
    Assert-Equal @($formatted.sources.Keys) $sourceKeys 'Source keys are alphabetically sorted'
    Assert-Equal $formatted.identifiers.tmdb $identifiers.tmdb 'Identifier metadata and field order are unchanged'
    Assert-Equal $formatted.sources.wikidata $sources.wikidata 'Source metadata and arrays are unchanged'
    Assert-Equal $formatted.custom 'retained' 'Formatting preserves unknown fields'
    Assert-Equal @($unsorted.identifiers.Keys) @('wikidata', 'tmdb', 'netflix', 'myAnimeList', 'imdb') 'Formatting does not mutate input maps'
    Assert-Equal (Format-FilmEntry $formatted) $formatted 'Formatting is idempotent'
    Assert-Equal (Format-FilmEntry ([ordered]@{ identifiers = @{}; sources = @{} })) ([ordered]@{ identifiers = [ordered]@{}; sources = [ordered]@{} }) 'Empty maps remain maps'
    Assert-Equal (Format-FilmEntry ([ordered]@{ identifiers = $null; sources = $null })) ([ordered]@{ identifiers = $null; sources = $null }) 'Null maps remain null'
    Assert-Equal (Format-FilmEntry ([ordered]@{ custom = 'only' })) ([ordered]@{ custom = 'only' }) 'Absent maps are not created'
    $single = Format-FilmEntry (New-Film @{ tmdb = @{ id = 20; kind = 'movie' } } @{ tmdb = @{ images = @{ small = 'poster' } } } @('imdb-lists'))
    Assert-Equal @($single.identifiers.Keys) @('tmdb') 'Single identifier remains a map'
    Assert-Equal @($single.sources.Keys) @('tmdb') 'Single source remains a map'

    $sortingPath = Join-Path $testPath 'sorting.yaml'
    $formatted | Export-Yaml -Path $sortingPath
    $sortedHash = (Get-FileHash $sortingPath).Hash
    $reorderedIdentifiers = [ordered]@{}
    $reorderedSources = [ordered]@{}
    foreach ($name in $identifierKeys) { $reorderedIdentifiers[$name] = $identifiers[$name] }
    foreach ($name in $sourceKeys) { $reorderedSources[$name] = $sources[$name] }
    $reordered = New-Film $reorderedIdentifiers $reorderedSources @('imdb-lists')
    $reordered['custom'] = 'retained'
    Format-FilmEntry $reordered | Export-Yaml -Path $sortingPath
    Assert-Equal (Get-FileHash $sortingPath).Hash $sortedHash 'Different insertion orders produce identical YAML bytes'
    $roundTripped = (Read-FilmsDatabase -LiteralPath $sortingPath)[0]
    Assert-Equal @($roundTripped.identifiers.Keys) $identifierKeys 'Written YAML preserves identifier key order'
    Assert-Equal @($roundTripped.sources.Keys) $sourceKeys 'Written YAML preserves source key order'
    @($unsorted, [ordered]@{ sources = [ordered]@{} }) | Export-Yaml -Path $sortingPath
    & "$scriptsPath/PruneFilms.ps1" -DatabasePath $sortingPath
    Assert-Equal (Get-FileHash $sortingPath).Hash $sortedHash 'Pruning normalizes retained records when writing YAML'

    $dbPath = Join-Path $testPath 'films.yaml'
    $legacy = [ordered]@{
        wikidata = 'Q1'
        title = [ordered]@{ en = 'Canonical Title'; ja = '日本語の作品' }
        type = @('television-series')
        country = @('Japan')
        language = @('ja')
        year = 2022
        imdb = [ordered]@{ id = 'tt001'; title = 'Old IMDb Title'; rating = 7.8; lists = [ordered]@{ queue = 32 } }
        myAnimeList = @{ id = 10 }
        tmdb = [ordered]@{ id = 20; kind = 'tv-series'; images = @{ small = 'old-poster' } }
        natively = @{ language = 'ja'; level = 30; temporaryLevel = $true; url = 'old-natively' }
        availability = @{ netflix = [ordered]@{
            id = 100; title = 'Old Netflix Title'; watchlist = $false; type = 'series'; year = '2022'
            countryDetails = [ordered]@{ JP = @{ originalAudio = @('ja'); audio = @('ja'); subtitle = @('ja') } }
        } }
        origin = @('imdb', 'netflix')
        custom = 'retained'
    }
    $watchlistOnly = @{ origin = @('netflix'); availability = @{ netflix = @{ id = 102; title = 'Watchlist'; watchlist = $false } } }
    $catalogOnly = @{ origin = @('netflix'); availability = @{ netflix = @{ id = 103; title = 'Catalog'; type = 'movie' } } }
    $ambiguous = @{ origin = @('netflix'); availability = @{ netflix = @{ id = 104; title = 'Ambiguous' } } }
    $noOrigins = @{ imdb = @{ id = 'tt005'; title = 'Cross Reference' }; availability = @{ netflix = @{ id = 105; title = 'No Origin' } } }
    $staleList = @{ origin = @('imdb'); imdb = @{ id = 'tt006'; title = 'Stale List' } }
    @($legacy, $watchlistOnly, $catalogOnly, $ambiguous, $noOrigins, $staleList) | Export-Yaml -Path $dbPath

    $rejected = $false
    try { $null = Read-FilmsDatabase -LiteralPath $dbPath } catch { $rejected = $_ -like '*MigrateFilmsDatabaseV2*' }
    Assert-True $rejected 'Operational scripts reject unmigrated data'
    $global:FilmTestRequests.Clear()
    $rejected = $false
    try { & "$scriptsPath/ImportNetflixCatalog.ps1" -DatabasePath $dbPath -ApiKey 'test-only' } catch { $rejected = $_ -like '*MigrateFilmsDatabaseV2*' }
    Assert-True $rejected 'Catalog rejects legacy data before any API usage'
    Assert-Equal $global:FilmTestRequests.Count 0 'Legacy catalog import sends no HTTP requests'

    & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath $dbPath
    $films = Read-FilmsDatabase -LiteralPath $dbPath
    Assert-Equal $films.Count 6 'Migration preserves record count'
    $film = $films[0]
    Assert-Equal @($film.Keys) @('origin', 'identifiers', 'sources', 'custom') 'Canonical key order and unknown fields'
    Assert-Equal @($film.identifiers.Keys) $identifierKeys 'Migration writes sorted identifier keys'
    Assert-Equal @($film.sources.Keys) $sourceKeys 'Migration writes sorted source keys'
    Assert-Equal $film.custom 'retained' 'Unknown metadata preserved'
    Assert-Equal $film.identifiers.wikidata.id 'Q1' 'Wikidata identifier moved'
    Assert-Equal $film.identifiers.netflix.id 100 'Netflix identifier moved without type change'
    Assert-Equal $film.identifiers.myAnimeList.id 10 'MyAnimeList identifier moved'
    Assert-Equal $film.identifiers.tmdb.kind 'tv-series' 'TMDB kind moved with identifier'
    Assert-Equal $film.sources.wikidata.title.ja '日本語の作品' 'Localized Wikidata title preserved'
    Assert-Equal $film.sources.wikidata.language @('ja') 'Single language remains an array'
    Assert-Equal $film.sources.imdb.rating 7.8 'IMDb metadata moved'
    Assert-Equal $film.sources.'imdb-lists'.lists.queue 32 'List rank moved'
    Assert-Equal $film.sources.imdb.title $film.sources.'imdb-lists'.title 'IMDb title copied to both sources'
    Assert-Equal $film.sources.'netflix-unogs'.title $film.sources.'netflix-watchlist'.title 'Netflix title copied to both sources'
    Assert-Equal $film.sources.tmdb.images.small 'old-poster' 'TMDB images moved'
    Assert-Equal $film.sources.natively.level 30 'Natively metadata moved'
    Assert-True (-not $film.sources.'netflix-unogs'.Contains('watchlist')) 'Catalog has no watchlist metadata'
    Assert-True (-not $film.sources.'imdb-lists'.Contains('id')) 'Source blocks have no shared IDs'
    Assert-Equal $film.origin @('imdb-lists', 'netflix-unogs', 'netflix-watchlist') 'Origins split and sorted'
    Assert-Equal $films[1].origin @('netflix-watchlist') 'Watchlist false still establishes origin'
    Assert-Equal $films[1].sources.'netflix-watchlist'.title 'Watchlist' 'Explicit watchlist false preserves title'
    Assert-Equal $films[2].origin @('netflix-unogs') 'Catalog-only origin inferred'
    Assert-Equal $films[3].origin @('netflix-unogs', 'netflix-watchlist') 'Ambiguous Netflix origin becomes both'
    foreach ($index in @(2, 3, 4)) {
        Assert-True (-not $films[$index].sources.Contains('netflix-watchlist')) 'Absent watchlist property does not create watchlist metadata'
    }
    Assert-True (-not $films[4].Contains('origin')) 'Sources alone do not invent origins'
    Assert-True (-not $films[5].Contains('origin')) 'Empty list membership does not retain IMDb origin'
    $hash = (Get-FileHash $dbPath).Hash
    & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath $dbPath
    Assert-Equal (Get-FileHash $dbPath).Hash $hash 'Migration is byte-idempotent'

    $watchlistMigrationPath = Join-Path $testPath 'watchlist-migration.yaml'
    @{ availability = @{ netflix = [ordered]@{ title = 'Active Watchlist'; watchlist = $true } } } | Export-Yaml -Path $watchlistMigrationPath
    & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath $watchlistMigrationPath
    $activeWatchlist = (Read-FilmsDatabase -LiteralPath $watchlistMigrationPath)[0]
    Assert-Equal $activeWatchlist.sources.'netflix-watchlist'.title 'Active Watchlist' 'Explicit watchlist true preserves title regardless of key order'
    Assert-Equal $activeWatchlist.sources.'netflix-watchlist'.watchlist $true 'Explicit watchlist true is preserved'

    $cacheMigrationPath = Join-Path $testPath 'cache-migration'
    foreach ($directory in @('sources/imdb/lists', 'sources/netflix', 'sources/tmdb', 'sources/natively')) {
        $null = New-Item -ItemType Directory -Path (Join-Path $cacheMigrationPath $directory) -Force
    }
    $cacheDb = Join-Path $cacheMigrationPath 'films.yaml'
    Copy-Item $dbPath $cacheDb
    Set-Content "$cacheMigrationPath/sources/imdb/lists/queue.csv" 'list-input'
    Set-Content "$cacheMigrationPath/sources/imdb/title.basics.tsv" 'imdb-data'
    Set-Content "$cacheMigrationPath/sources/netflix/netflix-catalog-countries.json" 'catalog-cache'
    Set-Content "$cacheMigrationPath/sources/netflix/netflix-my-list.json" 'watchlist-input'
    Set-Content "$cacheMigrationPath/sources/netflix/netflix-my-list-jp.json" 'localized-watchlist'
    Set-Content "$cacheMigrationPath/sources/netflix/notes.txt" 'unrelated-input'
    Set-Content "$cacheMigrationPath/sources/tmdb/tmdb-movies.json" 'tmdb-cache'
    Set-Content "$cacheMigrationPath/sources/natively/natively-movies-jpn.json" 'natively-cache'
    & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath $cacheDb
    Assert-True (Test-Path "$cacheMigrationPath/sources/imdb/lists/queue.csv") 'Cache migration is opt-in'
    Push-Location $cacheMigrationPath
    try {
        & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath 'films.yaml' -MigrateCaches
    } finally {
        Pop-Location
    }
    Assert-Equal (Get-Content "$cacheMigrationPath/sources/imdb-lists/queue.csv" -Raw).Trim() 'list-input' 'IMDb lists moved to separate source directory'
    Assert-Equal (Get-Content "$cacheMigrationPath/sources/netflix-unogs/netflix-catalog-countries.json" -Raw).Trim() 'catalog-cache' 'uNoGS cache moved without rewriting'
    Assert-True (Test-Path "$cacheMigrationPath/sources/netflix-watchlist/netflix-my-list.json") 'Watchlist input moved'
    Assert-True (Test-Path "$cacheMigrationPath/sources/netflix-watchlist/netflix-my-list-jp.json") 'Localized watchlist input moved'
    Assert-True (Test-Path "$cacheMigrationPath/sources/imdb/title.basics.tsv") 'IMDb dataset stays in place'
    Assert-True (Test-Path "$cacheMigrationPath/sources/tmdb/tmdb-movies.json") 'TMDB cache stays in place'
    Assert-True (Test-Path "$cacheMigrationPath/sources/natively/natively-movies-jpn.json") 'Natively cache stays in place'
    Assert-True (Test-Path "$cacheMigrationPath/sources/netflix/notes.txt") 'Unknown Netflix files stay in place'
    & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath $cacheDb -MigrateCaches
    Assert-True (-not (Test-Path "$cacheMigrationPath/sources/imdb/lists")) 'Repeated cache migration is harmless'
    $null = New-Item -ItemType Directory "$cacheMigrationPath/sources/imdb/lists"
    Set-Content "$cacheMigrationPath/sources/imdb/lists/queue.csv" 'conflicting-input'
    $legacy | Export-Yaml -Path $cacheDb
    $hash = (Get-FileHash $cacheDb).Hash
    $rejected = $false
    try { & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath $cacheDb -MigrateCaches } catch { $rejected = $_ -like '*already exists*' }
    Assert-True $rejected 'Cache destination collisions are explicit'
    Assert-Equal (Get-FileHash $cacheDb).Hash $hash 'Cache collision is detected before writing database'
    Assert-Equal (Get-Content "$cacheMigrationPath/sources/imdb-lists/queue.csv" -Raw).Trim() 'list-input' 'Cache collision cannot overwrite destination'

    $conflictPath = Join-Path $testPath 'conflict.yaml'
    @{ imdb = @{ id = 'tt001' }; identifiers = @{ imdb = @{ id = 'tt999' } } } | Export-Yaml -Path $conflictPath
    $hash = (Get-FileHash $conflictPath).Hash
    $rejected = $false
    try { & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath $conflictPath } catch { $rejected = $_ -like '*conflicting*' }
    Assert-True $rejected 'Mixed-version identifier conflicts are explicit'
    Assert-Equal (Get-FileHash $conflictPath).Hash $hash 'Conflict does not write database'
    @{ natively = 'invalid-source' } | Export-Yaml -Path $conflictPath
    $hash = (Get-FileHash $conflictPath).Hash
    $rejected = $false
    try { & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath $conflictPath } catch { $rejected = $_ -like '*non-dictionary source*' }
    Assert-True $rejected 'Malformed source metadata is rejected instead of discarded'
    Assert-Equal (Get-FileHash $conflictPath).Hash $hash 'Malformed data does not write database'
    @{ title = 'Old English'; 'title-ja' = '旧作品'; originCountry = @('Japan'); originalLanguage = @('ja'); providers = @{ netflix = @{ id = 200; title = 'Old' } }; origin = @('netflix') } | Export-Yaml -Path $conflictPath
    & "$scriptsPath/MigrateFilmsDatabase.ps1" -DatabasePath $conflictPath
    & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath $conflictPath
    $oldest = (Read-FilmsDatabase -LiteralPath $conflictPath)[0]
    Assert-Equal $oldest.sources.wikidata.title.ja '旧作品' 'V1-to-V2 and V2-to-V3 migrations can be chained'
    $hash = (Get-FileHash $conflictPath).Hash
    & "$scriptsPath/MigrateFilmsDatabase.ps1" -DatabasePath $conflictPath
    Assert-Equal (Get-FileHash $conflictPath).Hash $hash 'V1-to-V2 migration does not modify V3 data'

    Assert-Equal (Get-DisplayTitle $film) 'Canonical Title' 'Wikidata English display title'
    $fallback = New-Film @{ netflix = @{ id = 100 } } @{ 'netflix-watchlist' = @{ title = 'Watchlist Fallback' } } @('netflix-watchlist')
    Assert-Equal (Get-DisplayTitle $fallback) 'Watchlist Fallback' 'Display title fallback'
    Assert-Equal (Get-NetflixTitle $fallback) 'Watchlist Fallback' 'Netflix matching fallback'
    Assert-Equal @(Get-FilmOwnedIdentifiers $film).Count 2 'Netflix input origins share one owned ID'
    $secondary = New-Film @{ imdb = @{ id = 'tt001' } } @{ 'imdb-lists' = @{ lists = @{ other = 2 } } } @('imdb-lists')
    Assert-True (Merge-FilmRecords $film $secondary) 'Compatible records merge'
    Assert-Equal $film.sources.'imdb-lists'.lists.other 2 'List memberships union'
    $before = ConvertTo-Json $film -Depth 100 -Compress
    $secondary.identifiers.imdb.id = 'tt999'
    $mergeErrors = @()
    Assert-True (-not (Merge-FilmRecords $film $secondary -ErrorAction SilentlyContinue -ErrorVariable mergeErrors)) 'Conflicting shared ID blocks merge'
    Assert-True ($mergeErrors.Count -gt 0) 'Merge conflict reports an error'
    Assert-Equal (ConvertTo-Json $film -Depth 100 -Compress) $before 'Failed merge is nonmutating'
    $secondary = New-Film @{ tmdb = @{ id = 20; kind = 'movie' } } @{} @()
    Assert-True (-not (Merge-FilmRecords $film $secondary -ErrorAction SilentlyContinue)) 'Same TMDB ID with different kind cannot merge'

    $csvPath = Join-Path $testPath 'queue.csv'
    "Const,Title,Position`ntt001,List Title,5`ntt007,New List Title," | Set-Content $csvPath
    & "$scriptsPath/ImportIMDbList.ps1" -DatabasePath $dbPath -CsvPath $csvPath
    $films = Read-FilmsDatabase -LiteralPath $dbPath
    Assert-Equal $films[0].sources.'imdb-lists'.title 'List Title' 'List imports refresh only their title'
    Assert-Equal $films[0].sources.imdb.title 'Old IMDb Title' 'List import leaves IMDb dataset title'
    Assert-Equal $films[0].sources.'imdb-lists'.lists.queue 5 'List rank refreshed'
    Assert-True ($null -eq $films[6].sources.'imdb-lists'.lists.queue) 'Missing rank is null'
    "Const,Title,Position`ntt007,New List Title,1" | Set-Content $csvPath
    & "$scriptsPath/ImportIMDbList.ps1" -DatabasePath $dbPath -CsvPath $csvPath
    $films = Read-FilmsDatabase -LiteralPath $dbPath
    Assert-True ($films[0].origin -notcontains 'imdb-lists') 'Last imported list removes only list origin'
    Assert-Equal $films[0].identifiers.imdb.id 'tt001' 'List pruning preserves identifier'

    $watchlistPath = Join-Path $testPath 'watchlist.json'
    Write-Json @(@{ videoId = '100'; title = 'Watchlist Title' }, @{ videoId = '999'; title = 'Missing' }) $watchlistPath
    & "$scriptsPath/ImportNetflixWatchlist.ps1" -DatabasePath $dbPath -Path $watchlistPath
    $films = Read-FilmsDatabase -LiteralPath $dbPath
    Assert-Equal $films.Count 7 'Watchlist does not create missing IDs without Force'
    Assert-Equal $films[0].sources.'netflix-watchlist'.title 'Watchlist Title' 'Watchlist title refreshes without Force'
    Assert-Equal $films[0].sources.'netflix-unogs'.title 'Old Netflix Title' 'Watchlist leaves catalog title'
    & "$scriptsPath/ImportNetflixWatchlist.ps1" -DatabasePath $dbPath -Path $watchlistPath -Force
    $films = Read-FilmsDatabase -LiteralPath $dbPath
    Assert-Equal $films.Count 8 'Force creates a watchlist-only record'
    Assert-Equal $films[7].origin @('netflix-watchlist') 'New watchlist origin is input-specific'
    Write-Json @() $watchlistPath
    & "$scriptsPath/ImportNetflixWatchlist.ps1" -DatabasePath $dbPath -Path $watchlistPath
    $films = Read-FilmsDatabase -LiteralPath $dbPath
    Assert-Equal $films[0].sources.'netflix-watchlist'.watchlist $false 'Watchlist removal writes false'
    Assert-True ($films[0].origin -contains 'netflix-watchlist') 'Watchlist removal preserves retention'

    $cachePath = Join-Path $testPath 'netflix'
    $null = New-Item -ItemType Directory $cachePath
    Write-Json @{ results = @(@{ countrycode = 'US'; id = 78 }) } "$cachePath/netflix-catalog-countries.json"
    Write-Json @{ '100' = @{ results = @(@{ cc = 'US'; seasdet = 'S1:12'; newdate = '2022-01-01'; expiredate = '2026-12-31'; audio = 'English,Japanese [Original]'; subtitle = 'Japanese' }); timestamp = 'old' } } "$cachePath/netflix-catalog-titlecountries.json"
    Write-Json @{ '100' = @{ results = @(@{ matlabel = 'TV-MA'; img = 'small'; lgimg = 'large' }); timestamp = 'old' } } "$cachePath/netflix-catalog-titles.json"
    Write-Json @{ '100' = @{ results = @(@{ nfid = 7424; genre = 'Anime' }); timestamp = 'old' } } "$cachePath/netflix-catalog-titlegenres.json"
    $cacheHash = (Get-FileHash "$cachePath/netflix-catalog-titlecountries.json").Hash
    $global:FilmTestHttpMode = 'catalog'
    $global:FilmTestRequests.Clear()
    & "$scriptsPath/ImportNetflixCatalog.ps1" -DatabasePath $dbPath -CachePath $cachePath -ApiKey 'test-only' -RequestLimit 1
    $global:FilmTestHttpMode = 'blocked'
    Assert-Equal $global:FilmTestRequests.Count 1 'Catalog uses cached details, with only mocked search request'
    Assert-Equal (Get-FileHash "$cachePath/netflix-catalog-titlecountries.json").Hash $cacheHash 'Cached response remains untouched'
    $films = Read-FilmsDatabase -LiteralPath $dbPath
    Assert-Equal $films[0].sources.'netflix-unogs'.title 'Catalog Title' 'Catalog source refreshed'
    Assert-Equal $films[0].sources.'netflix-watchlist'.title 'Watchlist Title' 'Catalog leaves watchlist title'
    Assert-Equal $films[0].sources.'netflix-unogs'.countryDetails.US.originalAudio @('ja') 'Original audio remains an array'
    Assert-Equal $films[0].sources.'netflix-unogs'.countryDetails.US.subtitle @('ja') 'Subtitle remains an array'
    Assert-True ($films[0].sources.'netflix-unogs'.countryDetails.Contains('JP')) 'Country-scoped import preserves other countries'
    Assert-Equal @($films[0].sources.'netflix-unogs'.genres).Count 1 'Single genre remains an array'
    Assert-True (-not $films[0].sources.'netflix-unogs'.Contains('id')) 'Catalog metadata has no work identifier'
    Assert-Equal $films.Count 8 'Uncached title is skipped when request budget is exhausted'

    $imdbPath = Join-Path $testPath 'imdb'
    $null = New-Item -ItemType Directory $imdbPath
    "tconst`ttitleType`tprimaryTitle`toriginalTitle`tisAdult`tstartYear`tendYear`truntimeMinutes`tgenres`ntt001`ttvSeries`tDataset Title`t日本語の作品`t1`t2022`t2024`t24`tAnimation,Drama" | Set-Content "$imdbPath/title.basics.tsv"
    "tconst`taverageRating`tnumVotes`ntt001`t8.3`t100" | Set-Content "$imdbPath/title.ratings.tsv"
    "tconst`tparentTconst`tseasonNumber`tepisodeNumber`ntt1001`ttt001`t1`t1`ntt1002`ttt001`t\N`t\N" | Set-Content "$imdbPath/title.episode.tsv"
    "titleId`tordering`ttitle`tregion`tlanguage`ttypes`tattributes`tisOriginalTitle`ntt001`t1`tCatalog Title`tUS`ten`t\N`t\N`t0`ntt001`t2`t日本語の作品`tJP`tja`t\N`t\N`t1" | Set-Content "$imdbPath/title.akas.tsv"
    & "$scriptsPath/PopulateIMDb.ps1" -DatabasePath $dbPath -ImdbDataPath $imdbPath
    $films = Read-FilmsDatabase -LiteralPath $dbPath
    Assert-Equal $films[0].sources.imdb.title 'Dataset Title' 'IMDb data populates its source'
    Assert-Equal $films[0].sources.'imdb-lists'.title 'List Title' 'IMDb data leaves list title'
    Assert-Equal $films[0].sources.imdb.seasons 1 'Distinct known seasons counted'
    Assert-Equal $films[0].sources.imdb.episodes 2 'Unknown-season episodes included'
    Assert-Equal $films[0].sources.imdb.rating 8.3 'Rating refreshed'

    $tmdbPath = Join-Path $testPath 'tmdb'
    $null = New-Item -ItemType Directory $tmdbPath
    Write-Json @{ '20' = @{ id = 20; poster_path = '/poster.jpg' } } "$tmdbPath/tmdb-tv-series.json"
    Write-Json @{ 'tt007' = @{ movie_results = @(@{ id = 7 }); tv_results = @() } } "$tmdbPath/tmdb-imdb.json"
    Write-Json @{ '7' = @{ id = 7; poster_path = '/movie.jpg' } } "$tmdbPath/tmdb-movies.json"
    $tmdbDb = Join-Path $testPath 'tmdb.yaml'
    @($films[0], $films[6]) | Export-Yaml -Path $tmdbDb
    & "$scriptsPath/PopulateTMDB.ps1" -DatabasePath $tmdbDb -TmdbDataPath $tmdbPath -ApiKey ''
    $tmdbFilms = Read-FilmsDatabase -LiteralPath $tmdbDb
    Assert-Equal $tmdbFilms[0].sources.tmdb.images.small 'https://image.tmdb.org/t/p/w342/poster.jpg' 'Cached TMDB poster populated'
    Assert-Equal $tmdbFilms[1].identifiers.tmdb.kind 'movie' 'IMDb lookup supplies shared TMDB kind'
    Assert-Equal $tmdbFilms[1].identifiers.tmdb.id 7 'IMDb lookup supplies shared TMDB ID'

    $apiTestScriptsPath = Join-Path $testPath 'api-scripts'
    $apiTestFilmsPath = Join-Path $apiTestScriptsPath 'discover/films'
    $null = New-Item -ItemType Directory -Path $apiTestFilmsPath -Force
    $null = New-Item -ItemType Directory -Path "$apiTestScriptsPath/modules"
    foreach ($name in @('ImportNetflixCatalog.ps1', 'PopulateTMDB.ps1', 'tandoku-discover-films.psm1')) {
        Copy-Item "$scriptsPath/$name" $apiTestFilmsPath
    }
    foreach ($name in @('tandoku-yaml.psm1', 'tandoku-utils.psm1', 'tandoku-log.psm1')) {
        Copy-Item "$scriptsPath/../../modules/$name" "$apiTestScriptsPath/modules"
    }
    @'
param([string]$Key)
$global:FilmTestConfigKeys.Add($Key)
return $global:FilmTestConfig[$Key]
'@ | Set-Content "$apiTestScriptsPath/TandokuConfig.ps1"
    $apiTestDb = Join-Path $testPath 'api-keys.yaml'
    $previousRapidApiKey = $env:RAPIDAPI_KEY
    $previousTmdbApiKey = $env:TMDB_API_KEY
    try {
        $env:RAPIDAPI_KEY = 'env-rapidapi'
        $env:TMDB_API_KEY = 'env-tmdb'
        foreach ($case in @(
            @{ config = 'configured'; arguments = @{}; expected = 'configured'; reads = 2 },
            @{ config = ''; arguments = @{}; expected = 'env'; reads = 2 },
            @{ config = $null; arguments = @{}; expected = 'env'; reads = 2 },
            @{ config = 'configured'; arguments = @{ ApiKey = 'explicit' }; expected = 'explicit'; reads = 0 }
        )) {
            $global:FilmTestConfig = @{
                'rapidapi.apikey' = $case.config ? "$($case.config)-rapidapi" : $case.config
                'tmdb.apikey' = $case.config ? "$($case.config)-tmdb" : $case.config
            }
            $global:FilmTestConfigKeys.Clear()
            $global:FilmTestApiKeys.Clear()
            $keyArgs = $case.arguments
            $global:FilmTestHttpMode = 'catalog'
            & "$apiTestFilmsPath/ImportNetflixCatalog.ps1" -DatabasePath $dbPath -CachePath $cachePath -RequestLimit 1 @keyArgs
            $expectedRapidApiKey = $case.expected -eq 'explicit' ? 'explicit' : "$($case.expected)-rapidapi"
            Assert-Equal $global:FilmTestApiKeys[0] $expectedRapidApiKey 'Catalog API key precedence'
            $tmdbFilms[0] | Export-Yaml -Path $apiTestDb
            $global:FilmTestRequests.Clear()
            $global:FilmTestHttpMode = 'tmdb'
            & "$apiTestFilmsPath/PopulateTMDB.ps1" -DatabasePath $apiTestDb -TmdbDataPath $tmdbPath -UpdateTmdbData @keyArgs
            $expectedTmdbApiKey = $case.expected -eq 'explicit' ? 'explicit' : "$($case.expected)-tmdb"
            Assert-True ($global:FilmTestRequests[0].EndsWith("api_key=$expectedTmdbApiKey")) 'TMDB API key precedence'
            Assert-Equal $global:FilmTestConfigKeys.Count $case.reads 'Explicit API keys bypass TandokuConfig'
            if ($case.reads) {
                Assert-Equal @($global:FilmTestConfigKeys) @('rapidapi.apikey', 'tmdb.apikey') 'Scripts request the correct config keys'
            }
        }
    } finally {
        $env:RAPIDAPI_KEY = $previousRapidApiKey
        $env:TMDB_API_KEY = $previousTmdbApiKey
        $global:FilmTestHttpMode = 'blocked'
    }

    $nativelyPath = Join-Path $testPath 'natively'
    $null = New-Item -ItemType Directory $nativelyPath
    Write-Json @{ videos = @(@{ tmdbId = 7; tmdbKind = 'movie'; level = 25; url = 'movie-url'; temporary = $false; source = 'movie' }) } "$nativelyPath/natively-movies-jpn.json"
    Write-Json @{ videos = @(@{ tmdbId = 20; tmdbKind = 'tv-series'; level = 31; url = 'series-url'; temporary = $true; source = 'series' }) } "$nativelyPath/natively-tv-jpn.json"
    & "$scriptsPath/PopulateNatively.ps1" -DatabasePath $tmdbDb -NativelyDataPath $nativelyPath
    $tmdbFilms = Read-FilmsDatabase -LiteralPath $tmdbDb
    Assert-Equal $tmdbFilms[0].sources.natively.level 31 'Natively matches shared TMDB identity'
    Assert-Equal $tmdbFilms[0].sources.natively.temporaryLevel $true 'Temporary level preserved'
    Assert-True (-not $tmdbFilms[1].sources.natively.Contains('temporaryLevel')) 'Non-temporary level omits flag'

    $wikiDb = Join-Path $testPath 'wikidata.yaml'
    $wikiFilm = New-Film @{ imdb = @{ id = 'tt001' }; netflix = @{ id = 100 } } @{ imdb = @{ title = 'IMDb Title' }; tmdb = @{ images = @{ small = 'keep-poster' } } } @('imdb-lists', 'netflix-unogs', 'netflix-watchlist')
    $wikiFilm | Export-Yaml -Path $wikiDb
    $global:FilmTestQidBindings = @(
        @{ item = @{ value = 'http://www.wikidata.org/entity/Q1' }; id = @{ value = 'tt001' } },
        @{ item = @{ value = 'http://www.wikidata.org/entity/Q1' }; id = @{ value = '100' } }
    )
    $global:FilmTestBindings = @(@{
        item = @{ value = 'http://www.wikidata.org/entity/Q1' }
        titleEn = @{ value = 'Wiki Title' }; titleLang = @{ value = '作品' }
        types = @{ value = 'television series' }; country = @{ value = 'Japan' }
        language = @{ value = 'ja' }; startYear = @{ value = '2022' }
        imdbId = @{ value = 'tt999' }; netflixId = @{ value = '999' }
        malId = @{ value = '10~1~0|11~2~0' }; tmdbTvId = @{ value = '20' }
    })
    $global:FilmTestHttpMode = 'wikidata'
    $wikiErrors = @()
    $ErrorActionPreference = 'Continue'
    & "$scriptsPath/PopulateWikidata.ps1" -DatabasePath $wikiDb -ErrorAction Continue -ErrorVariable wikiErrors
    $ErrorActionPreference = 'Stop'
    $wikiFilm = (Read-FilmsDatabase -LiteralPath $wikiDb)[0]
    Assert-Equal $wikiFilm.identifiers.wikidata.id 'Q1' 'Wikidata lookup resolves shared imported IDs'
    Assert-Equal $wikiFilm.identifiers.imdb.id 'tt001' 'Conflicting Wikidata IMDb ID cannot overwrite import'
    Assert-Equal $wikiFilm.identifiers.netflix.id 100 'Conflicting Wikidata Netflix ID cannot overwrite either import'
    Assert-True ($wikiErrors.Count -ge 2) 'Identifier disagreement reports explicit errors'
    Assert-Equal $wikiFilm.sources.wikidata.title.en 'Wiki Title' 'Wikidata metadata is source-local'
    Assert-Equal $wikiFilm.sources.wikidata.language @('ja') 'Wikidata language stays an array'
    Assert-Equal $wikiFilm.identifiers.myAnimeList.id 10 'MyAnimeList qualifier selection preserved'
    Assert-Equal $wikiFilm.sources.tmdb.images.small 'keep-poster' 'Wikidata refresh does not erase another source'

    $global:FilmTestBindings = @(@{ item = @{ value = 'http://www.wikidata.org/entity/Q1' } })
    & "$scriptsPath/PopulateWikidata.ps1" -DatabasePath $wikiDb -Force
    $wikiFilm = (Read-FilmsDatabase -LiteralPath $wikiDb)[0]
    Assert-True (-not $wikiFilm.sources.wikidata.Contains('title')) 'Forced refresh removes missing Wikidata metadata'
    Assert-True (-not $wikiFilm.identifiers.Contains('tmdb')) 'Forced refresh removes missing non-owned cross-reference'
    Assert-Equal $wikiFilm.sources.tmdb.images.small 'keep-poster' 'Removing identifier does not delete source metadata'
    Assert-Equal $wikiFilm.identifiers.imdb.id 'tt001' 'Forced refresh preserves imported identifier'
    $global:FilmTestHttpMode = 'blocked'

    $syncPath = Join-Path $testPath 'sync'
    $null = New-Item -ItemType Directory "$syncPath/sources/imdb-lists" -Force
    $films[0] | Export-Yaml -Path "$syncPath/films.yaml"
    "Const,Title,Position`ntt001,Sync List Title,1" | Set-Content "$syncPath/sources/imdb-lists/queue.csv"
    Copy-Item $cachePath "$syncPath/sources/netflix-unogs" -Recurse
    Copy-Item $imdbPath "$syncPath/sources/imdb" -Recurse
    Copy-Item $tmdbPath "$syncPath/sources/tmdb" -Recurse
    Copy-Item $nativelyPath "$syncPath/sources/natively" -Recurse
    $null = New-Item -ItemType Directory "$syncPath/sources/netflix-watchlist"
    Write-Json @(@{ videoId = '100'; title = 'Sync Watchlist Title' }) "$syncPath/sources/netflix-watchlist/netflix-my-list.json"
    $previousApiKey = $env:RAPIDAPI_KEY
    try {
        $env:RAPIDAPI_KEY = 'test-only'
        $global:FilmTestHttpMode = 'catalog'
        & "$scriptsPath/SyncFilms.ps1" -Path $syncPath -NetflixCatalog -NetflixRequestLimit 1
    } finally {
        $env:RAPIDAPI_KEY = $previousApiKey
        $global:FilmTestHttpMode = 'blocked'
    }
    $synced = (Read-FilmsDatabase -LiteralPath "$syncPath/films.yaml")[0]
    Assert-Equal $synced.sources.'imdb-lists'.title 'Sync List Title' 'Sync uses new IMDb list directory'
    Assert-Equal $synced.sources.'netflix-unogs'.title 'Catalog Title' 'Sync uses new uNoGS cache directory'
    Assert-Equal $synced.sources.'netflix-watchlist'.title 'Sync Watchlist Title' 'Sync uses new watchlist directory'
    Assert-True (Test-Path "$syncPath/films.json") 'Sync exports V3 JSON'

    $suggestDb = Join-Path $testPath 'suggest.yaml'
    $fallback.sources.'netflix-watchlist'.title = 'Catalog Title'
    $fallback | Export-Yaml -Path $suggestDb
    $global:FilmTestHttpMode = 'wikidata'
    $global:FilmTestQidBindings = @(@{ item = @{ value = 'http://www.wikidata.org/entity/Q1' }; imdbId = @{ value = 'tt001' } })
    $candidatesPath = Join-Path $testPath 'candidates.yaml'
    & "$scriptsPath/SuggestWikidataIdentifiers.ps1" -DatabasePath $suggestDb -OutputPath $candidatesPath -ImdbDataPath $imdbPath
    $candidate = @(Import-Yaml -LiteralPath $candidatesPath)[0]
    Assert-Equal $candidate.netflix.id 100 'Candidate-review format remains unchanged'
    Assert-Equal $candidate.imdb[0].id 'tt001' 'Suggestion uses watchlist-title fallback'
    Assert-Equal $candidate.wikidata.id 'Q1' 'Suggestion resolves candidate QID'
    $global:FilmTestHttpMode = 'blocked'

    $jsonPath = Join-Path $testPath 'films.json'
    & "$scriptsPath/ExportFilms.ps1" -DatabasePath $suggestDb -OutputPath $jsonPath
    Assert-True ((Get-Content $jsonPath -Raw).TrimStart().StartsWith('[')) 'Single-record JSON export is an array'
    $emptyDb = Join-Path $testPath 'empty.yaml'
    Set-Content $emptyDb ''
    & "$scriptsPath/ExportFilms.ps1" -DatabasePath $emptyDb -OutputPath $jsonPath
    Assert-Equal (Get-Content $jsonPath -Raw).Trim() '[]' 'Empty JSON export is an array'
    & "$scriptsPath/PruneFilms.ps1" -DatabasePath $dbPath
    $films = Read-FilmsDatabase -LiteralPath $dbPath
    Assert-Equal $films.Count 6 'Pruning uses origins, not presence of source metadata'
    $noOrigins | Export-Yaml -Path $emptyDb
    & "$scriptsPath/MigrateFilmsDatabaseV2.ps1" -DatabasePath $emptyDb
    & "$scriptsPath/PruneFilms.ps1" -DatabasePath $emptyDb
    Assert-Equal (Read-FilmsDatabase -LiteralPath $emptyDb).Count 0 'Pruning every record empties the database'
    Write-Host "Passed $assertions assertions; all HTTP requests were mocked."
}
finally {
    if ($previousHttpFunction) {
        Set-Item Function:global:Invoke-RestMethod $previousHttpFunction.ScriptBlock
    } else {
        Remove-Item Function:global:Invoke-RestMethod
    }
    Remove-Variable FilmTestRequests, FilmTestHttpMode, FilmTestBindings, FilmTestQidBindings, FilmTestApiKeys, FilmTestConfig, FilmTestConfigKeys -Scope Global
    Remove-Item -LiteralPath $testPath -Recurse -Force
}
