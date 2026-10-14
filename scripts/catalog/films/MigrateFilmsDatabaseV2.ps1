[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DatabasePath,

    [switch]$MigrateCaches
)

Import-Module "$PSScriptRoot/../../modules/tandoku-yaml.psm1"
Import-Module "$PSScriptRoot/tandoku-discover-films.psm1"

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

function Merge-MigrationFields($target, $incoming, [string]$path) {
    foreach ($key in $incoming.Keys) {
        if (-not $target.Contains($key) -or $null -eq $target[$key]) {
            $target[$key] = $incoming[$key]
        } elseif ($target[$key] -is [System.Collections.IDictionary] -and $incoming[$key] -is [System.Collections.IDictionary]) {
            Merge-MigrationFields $target[$key] $incoming[$key] "$path.$key"
        } elseif ($null -ne $incoming[$key] -and
            (ConvertTo-Json -InputObject $target[$key] -Depth 100 -Compress) -cne
            (ConvertTo-Json -InputObject $incoming[$key] -Depth 100 -Compress)) {
            throw "Cannot migrate conflicting V2 and V3 values at $path.$key. Resolve the conflict before retrying; the database has not been written."
        }
    }
}

function Move-Identifier($film, [string]$name, $id, [string]$kind) {
    if ($null -eq $id -or $id -eq '') { return }
    if (-not $film.Contains('identifiers') -or $null -eq $film['identifiers']) {
        $film['identifiers'] = [ordered]@{}
    }
    if (-not $film['identifiers'].Contains($name) -or $null -eq $film['identifiers'][$name]) {
        $film['identifiers'][$name] = [ordered]@{}
    }
    $incoming = [ordered]@{ id = $id }
    if ($kind) { $incoming['kind'] = $kind }
    Merge-MigrationFields $film['identifiers'][$name] $incoming "identifiers.$name"
}

function Move-Source($film, [string]$name, $incoming) {
    if ($incoming -isnot [System.Collections.IDictionary]) {
        throw "Cannot migrate non-dictionary source '$name'. The database has not been written."
    }
    if ($incoming.Count -eq 0) { return }
    $target = Get-FilmSource $film $name -Create
    Merge-MigrationFields $target $incoming "sources.$name"
}

# Convert a V2 entry to V3; already-migrated V3 entries are left unchanged.
function Migrate-FilmEntryV2($film) {
    foreach ($key in @('identifiers', 'sources', 'availability')) {
        if ($film.Contains($key) -and $null -ne $film[$key] -and $film[$key] -isnot [System.Collections.IDictionary]) {
            throw "Cannot migrate non-dictionary '$key'. The database has not been written."
        }
    }
    foreach ($key in @('title-ja', 'originCountry', 'originalLanguage', 'providers')) {
        if ($film.Contains($key)) {
            throw "Original V1 field '$key' found. Run MigrateFilmsDatabase.ps1 (V1 to V2) before MigrateFilmsDatabaseV2.ps1 (V2 to V3)."
        }
    }
    if ($film.Contains('title') -and $null -ne $film['title'] -and $film['title'] -isnot [System.Collections.IDictionary]) {
        throw 'V1 scalar title found. Run MigrateFilmsDatabase.ps1 (V1 to V2) before MigrateFilmsDatabaseV2.ps1 (V2 to V3).'
    }

    $legacyKeys = @('wikidata', 'title', 'type', 'country', 'language', 'year', 'imdb', 'myAnimeList', 'tmdb', 'natively', 'availability')
    $hasLegacyFields = @($legacyKeys | Where-Object { $film.Contains($_) }).Count -gt 0
    $oldOrigins = @($film.origin | Where-Object { $_ })
    if (-not $hasLegacyFields -and @($oldOrigins | Where-Object { $_ -in @('imdb', 'netflix') }).Count -eq 0) {
        return $false
    }

    Move-Identifier $film 'wikidata' $film.wikidata
    $wikidata = [ordered]@{}
    foreach ($key in @('title', 'type', 'country', 'language', 'year')) {
        if ($film.Contains($key)) { $wikidata[$key] = $film[$key] }
    }
    Move-Source $film 'wikidata' $wikidata

    foreach ($name in @('imdb', 'myAnimeList', 'tmdb')) {
        if (-not $film.Contains($name) -or $null -eq $film[$name]) { continue }
        $legacy = $film[$name]
        if ($legacy -isnot [System.Collections.IDictionary]) {
            throw "Cannot migrate non-dictionary '$name'. The database has not been written."
        }
        $kind = if ($name -eq 'tmdb') { $legacy['kind'] } else { $null }
        Move-Identifier $film $name $legacy['id'] $kind
        $metadata = [ordered]@{}
        foreach ($key in $legacy.Keys) {
            if ($key -eq 'id' -or ($name -eq 'tmdb' -and $key -eq 'kind') -or ($name -eq 'imdb' -and $key -eq 'lists')) { continue }
            $metadata[$key] = $legacy[$key]
        }
        Move-Source $film $name $metadata
        if ($name -eq 'imdb') {
            $lists = [ordered]@{}
            foreach ($key in @('title', 'lists')) {
                if ($legacy.Contains($key)) { $lists[$key] = $legacy[$key] }
            }
            Move-Source $film 'imdb-lists' $lists
        }
    }

    if ($film.Contains('natively') -and $null -ne $film['natively']) {
        Move-Source $film 'natively' $film['natively']
    }

    $netflix = $null
    if ($film.availability) {
        foreach ($name in $film.availability.Keys) {
            if ($name -ne 'netflix') {
                throw "Cannot migrate unknown availability provider '$name'. The database has not been written."
            }
        }
        $netflix = $film.availability.netflix
    }
    $hasCatalog = $false
    $hasWatchlist = $false
    if ($null -ne $netflix) {
        if ($netflix -isnot [System.Collections.IDictionary]) {
            throw 'Cannot migrate non-dictionary availability.netflix. The database has not been written.'
        }
        Move-Identifier $film 'netflix' $netflix['id']
        $catalog = [ordered]@{}
        $watchlist = [ordered]@{}
        foreach ($key in $netflix.Keys) {
            if ($key -eq 'id') { continue }
            if ($key -eq 'watchlist') {
                $watchlist[$key] = $netflix[$key]
                $hasWatchlist = $true
            } else {
                $catalog[$key] = $netflix[$key]
                if ($key -eq 'title') {
                    if ($netflix.Contains('watchlist')) {
                        $watchlist[$key] = $netflix[$key]
                    }
                } else {
                    $hasCatalog = $true
                }
            }
        }
        Move-Source $film 'netflix-unogs' $catalog
        Move-Source $film 'netflix-watchlist' $watchlist
    }

    Remove-Origin $film 'imdb'
    Remove-Origin $film 'netflix'
    if ($oldOrigins -contains 'imdb') {
        $lists = Get-FilmSource $film 'imdb-lists'
        if ($lists -and $lists.lists -and $lists.lists.Count -gt 0) {
            Add-Origin $film 'imdb-lists'
        }
    }
    if ($oldOrigins -contains 'netflix') {
        if ($hasCatalog -or -not $hasWatchlist) { Add-Origin $film 'netflix-unogs' }
        if ($hasWatchlist -or -not $hasCatalog) { Add-Origin $film 'netflix-watchlist' }
    }

    foreach ($key in $legacyKeys) { $film.Remove($key) }
    return $true
}

function Get-CacheMigrationPlan([string]$sourcesPath) {
    $moves = [System.Collections.Generic.List[object]]::new()
    $listsPath = Join-Path $sourcesPath 'imdb/lists'
    if (Test-Path -LiteralPath $listsPath -PathType Container) {
        $moves.Add([pscustomobject]@{ From = $listsPath; To = (Join-Path $sourcesPath 'imdb-lists') })
    }
    $netflixPath = Join-Path $sourcesPath 'netflix'
    if (Test-Path -LiteralPath $netflixPath -PathType Container) {
        foreach ($file in (Get-ChildItem -LiteralPath $netflixPath -File | Sort-Object Name)) {
            $destination = if ($file.Name -like 'netflix-catalog-*.json') {
                'netflix-unogs'
            } elseif ($file.Name -like 'netflix-my-list*.json') {
                'netflix-watchlist'
            } else {
                $null
            }
            if ($destination) {
                $moves.Add([pscustomobject]@{ From = $file.FullName; To = (Join-Path $sourcesPath "$destination/$($file.Name)") })
            }
        }
    }
    foreach ($move in $moves) {
        $parent = Split-Path $move.To -Parent
        if ((Test-Path -LiteralPath $parent) -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
            throw "Cannot migrate cache '$($move.From)': destination parent '$parent' is not a directory. No files have been moved or written."
        }
        if (Test-Path -LiteralPath $move.To) {
            throw "Cannot migrate cache '$($move.From)': destination '$($move.To)' already exists. Resolve the collision before retrying; no files have been moved or written."
        }
    }
    return , $moves
}

$cacheMoves = [System.Collections.Generic.List[object]]::new()
$DatabasePath = (Get-Item -LiteralPath $DatabasePath).FullName
if ($MigrateCaches) {
    $databaseDirectory = Split-Path $DatabasePath -Parent
    $cacheMoves = Get-CacheMigrationPlan (Join-Path $databaseDirectory 'sources')
}

$films = Read-FilmsDatabase -LiteralPath $DatabasePath -AllowLegacy
$migrated = 0
for ($i = 0; $i -lt $films.Count; $i++) {
    if (Migrate-FilmEntryV2 $films[$i]) { $migrated++ }
    $films[$i] = Format-FilmEntry $films[$i]
}
if ($migrated -gt 0) {
    $temporaryPath = "$DatabasePath.$([guid]::NewGuid()).tmp"
    try {
        $films | Export-Yaml -Path $temporaryPath
        Move-Item -LiteralPath $temporaryPath -Destination $DatabasePath -Force
    } finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath
        }
    }
}
foreach ($move in $cacheMoves) {
    $parent = Split-Path $move.To -Parent
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $parent
    }
    Move-Item -LiteralPath $move.From -Destination $move.To -ErrorAction Stop
    Write-Host "Moved $($move.From) -> $($move.To)"
}
if ($MigrateCaches) {
    Write-Host "Moved $($cacheMoves.Count) cache/input path(s); other source directories were left unchanged."
}
Write-Host "Done: migrated $migrated of $($films.Count) entries from V2 to V3 in $DatabasePath"
