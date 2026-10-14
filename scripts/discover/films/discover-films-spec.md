# discover films
Tools for compiling a database to aid discovery and ranking of Japanese films (including movies and TV series) for language learning.

# Data
## `films.yaml`
YAML stream containing one document per film (movie or TV series), in V3 format.
`origin` records importing inputs, `identifiers` holds shared provider IDs, and
`sources` separates each input's metadata. See the
[full record and field reference](films-record-proposal.md) for all fields.
Run `MigrateFilmsDatabaseV2.ps1` before using these scripts on an existing V2
database; operational scripts reject unmigrated records instead of mixing formats.

When writing records, scripts use the top-level order `origin`, `identifiers`,
`sources`, followed by any unknown fields. Keys directly under `identifiers`
and `sources` are alphabetically sorted to avoid insertion-order diffs; fields
inside each identifier or source block retain their existing order.

| Format | Structure | Migration |
| --- | --- | --- |
| V1 (original) | Scalar `title`, `title-ja`, `originCountry`, `originalLanguage`, `providers` | `MigrateFilmsDatabase.ps1`: V1 to V2 |
| V2 | Language-keyed `title`, `country`, `language`, `availability`; shared IDs mixed with source metadata | `MigrateFilmsDatabaseV2.ps1`: V2 to V3 |
| V3 (current) | `origin`, shared `identifiers`, and separate `sources` | Current workflow format |

The migration script filenames are unchanged; the `V2` suffix names the input
format for the V2-to-V3 migration, not its output format.

```yaml
origin: [imdb-lists, netflix-watchlist]
identifiers:
  imdb:
    id: tt36592690
  myAnimeList:
    id: 59845
  netflix:
    id: 82024665
  tmdb:
    id: 271607
    kind: tv-series
  wikidata:
    id: Q130305455
sources:
  imdb:
    rating: 8.3
    votes: 12000
  imdb-lists:
    title: The Fragrant Flower Blooms with Dignity
    lists:
      queue-japanese: 3
      completed-japanese-intensive-immersion: 12
  natively:
    language: ja
    level: 21
    url: https://learnnatively.com/tv/66797d747a/
  netflix-watchlist:
    title: The Fragrant Flower Blooms With Dignity
    watchlist: true
  tmdb:
    images:
      small: https://image.tmdb.org/t/p/w342/example.jpg
      large: https://image.tmdb.org/t/p/w780/example.jpg
  wikidata:
    title:
      en: The Fragrant Flower Blooms with Dignity
      ja: 薫る花は凛と咲く
    type: [anime-television-series]
    country: [Japan]
    language: [ja]
    year: 2025
---
origin: [netflix-watchlist]
identifiers:
  imdb:
    id: tt0399971
  netflix:
    id: 81922646
  tmdb:
    id: 2146
    kind: tv-series
  wikidata:
    id: Q626942
sources:
  imdb:
    rating: 7.5
    votes: 830
  natively:
    language: ja
    level: 30
    temporaryLevel: true
    url: https://learnnatively.com/tv/8c14b5b860/
  netflix-watchlist:
    title: Good Luck!!
    watchlist: true
  wikidata:
    title:
      en: Good Luck!!
      ja: GOOD LUCK!!
    type: [japanese-television-drama]
    country: [Japan]
    language: [ja]
    year: 2003
```

# Scripts
The scripts in this folder share helpers through the `tandoku-discover-films.psm1`
module (e.g. `Read-FilmsDatabase`, `Format-FilmEntry`, `Add-Origin`). Functions
used by more than one script belong in this module rather than being copied into
each script.

Display titles prefer Wikidata English, then another Wikidata label, then IMDb,
uNoGS, Netflix watchlist, and IMDb lists. Netflix title matching prefers uNoGS
then watchlist. These are read-time fallbacks, not stored canonical copies.

Scripts use PowerShell, `powershell-yaml`, and `yq`. Run the offline regression
tests with `pwsh -NoProfile -File scripts/discover/films/tests/FilmRecords.Tests.ps1`
from the repository root. All HTTP in those tests is mocked.

## ImportNetflixWatchlist.ps1
### Usage
```powershell
ImportNetflixWatchlist.ps1 -Path <netflix-my-list.json> -DatabasePath <films.yaml> [-Force]
```

### Parameters
- `-Path` - Path to the netflix-my-list.json created by [Netflix Watch List Manager](https://chromewebstore.google.com/detail/netflix-watch-list-manage/obgidigipndchfoaapdbldekffjpmmfa).
- `-DatabasePath` - Path to the films.yaml database file.
- `-Force` - Allows creating a record for a Netflix ID not already present.

### Behavior
Matches records by `identifiers.netflix.id`, refreshing
`sources.netflix-watchlist.title` and setting its `watchlist` to `true`.
Adds the `netflix-watchlist` origin. Missing records are skipped with a warning
unless `-Force` is supplied; new records store the shared Netflix ID.
Previously listed items absent from the input become `watchlist: false` but
keep their origin, preserving retention. Titles now refresh without `-Force`,
since the watchlist can no longer overwrite the catalog title.

## ImportNetflixCatalog.ps1
### Usage
```powershell
ImportNetflixCatalog.ps1 -DatabasePath <films.yaml> [-Country <codes>] [-AudioLanguage <code>] [-AnyAudio] [-SubtitleLanguage <code>] [-CachePath <dir>] [-RequestLimit <n>] [-ApiKey <key>]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-Country` - One or more ISO 3166-1 alpha-2 country codes (defaults to `US`). Titles available in at least one of these countries are imported, and per-country availability details are retrieved for each of them.
- `-AudioLanguage` - Two-letter ISO 639-1 audio language code to filter on (defaults to `ja`). Pass an empty string to skip the audio filter.
- `-AnyAudio` - Includes dubbed audio; without it the selected language must be an original audio track in a requested country.
- `-SubtitleLanguage` - Two-letter ISO 639-1 subtitle language code to filter on (no default). When both `-AudioLanguage` and `-SubtitleLanguage` are specified, titles must match both (AND filter).
- `-CachePath` - Optional folder for caching uNoGS responses across runs. The Countries response is cached to `netflix-catalog-countries.json`, and Title Countries responses to `netflix-catalog-titlecountries.json` (a Netflix-id-keyed map, sorted by id, where each entry has `results` and a `timestamp`). Cached lookups do not count against `-RequestLimit`.
- `-RequestLimit` - Maximum number of requests sent to the uNoGS API across all endpoints in a single run (defaults to `100`; `0` means no limit). Once the limit is reached the script emits a single warning and skips all remaining requests, leaving the rest for a later run. Combined with `-CachePath`, a large catalog can be imported incrementally across multiple runs.
- `-ApiKey` - uNoGS RapidAPI key. When omitted, reads `rapidapi.apikey` from TandokuConfig, falling back to the `RAPIDAPI_KEY` environment variable if no value is configured.

### Behavior
Queries the [uNoGS API](https://rapidapi.com/unogs/api/unogsng) for Netflix titles and imports them into films.yaml by adding or updating entries matched by `identifiers.netflix.id`. Uses the Search endpoint (resolving country codes to uNoGS country IDs via the Countries endpoint) to find titles available in the requested countries with the requested audio/subtitle languages, paging through all results. For each matching title it retrieves Title Countries, Title Details, and Title Genres, using the supplied cache when available.

Stores the shared `identifiers.netflix.id`, and populates `sources.netflix-unogs`
with `title`, `type` (`movie` or `series`), `year`, `matlabel`, `images`, `genres`,
and a `countryDetails` dictionary keyed by country code. Each country has
optional `seasonDetails`/`expireDate`, `newDate`, and `originalAudio`, `audio`,
and `subtitle` arrays of normalized, deduplicated, sorted language codes.
Country details outside the current run are preserved. The `netflix-unogs`
origin is added, without modifying watchlist metadata. Vanished catalog titles
are not pruned automatically.

API usage is capped by `-RequestLimit`; uncached lookups are skipped once it is reached (titles without availability details are left untouched for a later run). With `-CachePath`, Countries and Title Countries responses are cached so successive runs make fewer requests and can resume where a previous run stopped, making it practical to import a large catalog incrementally.

```yaml
origin: [netflix-unogs]
identifiers:
  netflix:
    id: 81249833
sources:
  netflix-unogs:
    title: VINLAND SAGA
    type: series
    year: 2019
    countryDetails:
      US:
        seasonDetails: "S1:24,S2:24"
        newDate: "2022-07-10"
        originalAudio: [ja]
        audio: [de, en, es, fr, ja]
        subtitle: [en, es, ja, zh]
```

## DownloadIMDbLists.ps1
### Usage
```powershell
DownloadIMDbLists.ps1 -IMDbExportsPath <imdb-exports.html> -CsvPath <dir>
```

### Parameters
- `-IMDbExportsPath` - Path to a saved IMDb exports page (`https://www.imdb.com/exports/`). IMDb only serves this page behind a JavaScript/anti-bot challenge, so it must be saved from a logged-in browser session rather than fetched directly.
- `-CsvPath` - Path to a directory the downloaded list CSV files are written to (created if it does not exist).

### Behavior
Parses the saved exports page's embedded `__NEXT_DATA__` JSON to find every ready title-list export (`exportType` `LIST`, `listType` `TITLES`, `status` `READY`), keeping the most recent export per list id. For each list it downloads the export's CSV from the presigned download URL embedded in the page and saves it as `<kebab-cased list name>.csv` under `-CsvPath`, so the file name doubles as the list name when imported by `ImportIMDbList.ps1`.

Lists are processed in a stable order (sorted by slug, then list id) so that when two distinct list names reduce to the same kebab-cased slug, the collision suffixes (`<slug>.csv`, `<slug>-2.csv`, ...) are assigned deterministically across runs; a warning is emitted for each collision.

> **Note:** the presigned CSV download URLs embedded in the exports page expire a few minutes after the page is loaded. Save the exports page and run this script promptly; if the downloads fail with an expired/forbidden error, re-save the page and try again.

## ImportIMDbList.ps1
### Usage
```powershell
ImportIMDbList.ps1 -DatabasePath <films.yaml> -CsvPath <paths>
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-CsvPath` - One or more paths to import list CSVs from. Each element may be a directory (all `*.csv` files within it are used), a single CSV file, or a wildcard pattern (e.g. `lists-*.csv`). Resolved files are deduplicated.

### Behavior
Imports one or more IMDb list CSV exports (as produced by `DownloadIMDbLists.ps1`) into films.yaml. Each CSV's filename stem is the list name under `sources.imdb-lists.lists.<list-name>: <rank>`, where rank is the CSV `Position` (or null) and the IMDb ID is `Const`. Matches by `identifiers.imdb.id`, creating records when necessary, refreshes `sources.imdb-lists.title`, and adds the `imdb-lists` origin.

Repeated runs only set the imported list's membership, leaving other lists and
all other sources intact. A warning is emitted when distinct CSV files resolve
to the same list name.

Each imported list is authoritative for its own membership: absent titles lose
that list's entry. When no list memberships remain, `lists` and the `imdb-lists`
origin are removed; the identifier and source title are retained. Only lists
actually imported in the run are pruned.

## PopulateWikidata.ps1
### Usage
```powershell
PopulateWikidata.ps1 -DatabasePath <films.yaml> [-Language <code>] [-Force]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-Language` - Two-letter ISO 639-1 language code for the localized title fetched alongside English (defaults to `ja`). Populates `sources.wikidata.title.en` and `sources.wikidata.title.<Language>`.
- `-Force` - When specified, re-runs both phases for every entry even if the data is already present, refreshing existing values and removing fields that Wikidata no longer returns. Without it, the script only fills in missing data.

### Behavior
Lookup is **origin-aware**: `imdb-lists` backs `identifiers.imdb.id` (P345);
either Netflix input backs the shared `identifiers.netflix.id` (P1874).
The two Netflix origins contribute only one identifier to lookup.

1. **QID lookup** - Resolves missing `identifiers.wikidata.id` from imported
   identifiers. Conflicting QIDs produce an error without changing the stored
   QID. `-Force` repeats lookup for records with an imported identifier.
2. **Merge duplicates** - Records with the same QID union origins and source
   dictionaries, including list memberships. Any conflicting shared ID or TMDB
   kind blocks the merge before mutation and produces an error.
3. **Details enrichment** - For a known QID with missing
   `sources.wikidata.title`, or every known QID with `-Force`, populates
   `sources.wikidata.title`, `type` (P31 labels, kebab-cased), `country` (P495
   names), `language` (P364/P424, with P407/P424 fallback), and `year` (P580,
   with P577 fallback). Populates cross-references under `identifiers.imdb`
   (P345), `identifiers.myAnimeList` (P4086), and `identifiers.tmdb`
   (P4947/P4983 plus `kind`). IMDb IDs backed by list origins and Netflix IDs
   backed by either Netflix origin are only verified, never overwritten.
   Netflix IDs are not populated from Wikidata.

Multiple IMDb IDs select the lowest string value with a warning. MyAnimeList
selection excludes deprecated statements, prefers IDs without a P1810 qualifier,
and uses the lowest P1545 ordinal when all remaining candidates have one;
otherwise it warns on ambiguity and selects the lowest numeric ID. TMDB prefers
TV-series IDs over movie IDs and the lowest numeric value, warning on ambiguity.
`-Force` removes missing Wikidata fields and missing non-owned cross-reference
identifiers, but does not delete other sources' metadata. Records without origins
are not looked up by imported identifier; existing QIDs can still be enriched.
A warning recommends `PruneFilms.ps1` for records without origins.

## PruneFilms.ps1
### Usage
```powershell
PruneFilms.ps1 -DatabasePath <films.yaml>
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.

### Behavior
Removes stale film records that no longer have any origin (a missing or empty `origin` list). A record can lose its origins when, for example, an IMDb-list-only film is pruned out of every list it belonged to (see `ImportIMDbList.ps1`); such a record no longer corresponds to any source. Each removed record is logged, and the database is only rewritten when at least one record is removed.

## UpdateIMDbData.ps1
### Usage
```powershell
UpdateIMDbData.ps1 -ImdbDataPath <path> -Datasets <names> [-UpdateImdbData]
```

### Parameters
- `-ImdbDataPath` - Path to a local folder for storing IMDb data files downloaded from https://datasets.imdbws.com.
- `-Datasets` - One or more IMDb dataset names to make available, given without the `.tsv.gz` suffix (e.g. `title.ratings`, `title.basics`, `title.akas`).
- `-UpdateImdbData` - When specified, re-downloads (and re-extracts) the data files even if they already exist locally.

### Behavior
Shared helper used by the other IMDb-consuming scripts. For each requested dataset, downloads `<dataset>.tsv.gz` from the IMDb daily data dumps into `-ImdbDataPath` and extracts it to `<dataset>.tsv`. Existing `.tsv` files are reused unless `-UpdateImdbData` is specified; a previously downloaded `.gz` is reused (and only re-extracted) when its `.tsv` is missing. Extraction is written to a temporary file and moved into place so an interrupted run never leaves a partial `.tsv`. Returns an ordered hashtable mapping each dataset name to the full path of its extracted `.tsv` file.

## PopulateIMDb.ps1
### Usage
```powershell
PopulateIMDb.ps1 -DatabasePath <films.yaml> -ImdbDataPath <path> [-UpdateImdbData]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-ImdbDataPath` - Path to a local folder for storing IMDb data files downloaded from https://datasets.imdbws.com.
- `-UpdateImdbData` - When specified, re-downloads IMDb data files even if they already exist locally.

### Behavior
Uses `UpdateIMDbData.ps1` for `title.basics`, `title.ratings`, and `title.episode`.
Matches `identifiers.imdb.id`, populating `sources.imdb` with title,
originalTitle, type, adult, year/endYear, runtime, genres, season/episode counts,
rating, and votes. List metadata is untouched. Existing source fields not
overwritten by a retrieved value are retained, as in the original workflow.

## PopulateTMDB.ps1
### Usage
```powershell
PopulateTMDB.ps1 -DatabasePath <films.yaml> -TmdbDataPath <path> [-ApiKey <key>] [-UpdateTmdbData]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-TmdbDataPath` - Path to the directory used to cache complete TMDB API responses in `tmdb-imdb.json`, `tmdb-movies.json`, and `tmdb-tv-series.json`.
- `-ApiKey` - TMDB API key. When omitted, reads `tmdb.apikey` from TandokuConfig, falling back to the `TMDB_API_KEY` environment variable if no value is configured. Only required for uncached lookups.
- `-UpdateTmdbData` - When specified, refreshes cached responses from TMDB.

### Behavior
Retrieves a poster for every film record. Records with both `identifiers.tmdb.id` and `identifiers.tmdb.kind` are fetched directly as a TMDB movie or TV series; otherwise, the script uses `identifiers.imdb.id` with TMDB's external-ID lookup and fills in the shared TMDB ID/kind. IMDb source type disambiguates movie versus TV-series results when possible. When a stored TMDB ID returns HTTP 404, the script warns and retries via IMDb when available. A warning is emitted when neither identifier is available, no unambiguous TMDB match exists, or the matched title has no poster.

The complete JSON response from each TMDB lookup is cached under `-TmdbDataPath`, rather than caching only the selected image fields, so future TMDB metadata can be extracted without repeating API calls. IMDb find responses, movie details, and TV-series details are stored as ID-keyed dictionaries in `tmdb-imdb.json`, `tmdb-movies.json`, and `tmdb-tv-series.json`, respectively. Each dictionary is sorted by ID for stable diffs and saved after every new response. Cached data is reused unless `-UpdateTmdbData` is supplied. The selected poster is written under `sources.tmdb.images` as full `small` (`w342`) and `large` (`w780`) image URLs.

## SuggestWikidataIdentifiers.ps1
### Usage
```powershell
SuggestWikidataIdentifiers.ps1 -DatabasePath <films.yaml> -OutputPath <candidates.yaml> -ImdbDataPath <path> [-UpdateImdbData]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-OutputPath` - Path to the YAML file to write proposed candidates to.
- `-ImdbDataPath` - Path to a local folder for storing IMDb data files downloaded from https://datasets.imdbws.com.
- `-UpdateImdbData` - When specified, re-downloads IMDb data files even if they already exist locally.

### Behavior
Proposes candidates for records with `identifiers.netflix.id` but no
`identifiers.wikidata.id`. Matches the uNoGS title, falling back to the watchlist
title, using normalization (lowercase, non-letter/non-digit runs become spaces,
trimmed). A warning is emitted when records share the same normalized title.
The candidate-review stream below is separate from `films.yaml` and its format
is unchanged; `CommitWikidataIdentifiers.ps1` still consumes it.

Uses `UpdateIMDbData.ps1` to fetch `title.akas`, `title.basics`, and `title.ratings`, then makes one streaming pass over each:

1. **akas** - alternate titles are grouped by `titleId` (rows for a title are contiguous in the file); a title is a candidate when any of its alternate titles matches a needed Netflix title. While scanning the group it also records a Japanese-language signal (an alternate title tagged language `ja`, or one containing Hiragana/Katakana/Han characters) - this catches Japanese works whose matched title is the English/romaji one while the Japanese title appears in a separate row.
2. **basics** - additionally matches each title's `primaryTitle`/`originalTitle`, and for every candidate records its `titleType` and whether its `originalTitle` contains Japanese text.
3. **ratings** - records vote counts for candidate titles (used only as a tie-breaker).

Candidate IMDb titles are kept when their `titleType` is a watchable type (`movie`, `tvMovie`, `tvSeries`, `tvMiniSeries`, `short`, `tvShort`, `tvSpecial`, `video`) **and** they carry a Japanese signal (from either the akas or the original title). For each film the best surviving candidate is chosen by preferred title type, then most votes, then lowest IMDb ID; a warning lists the alternatives whenever more than one candidate survives. The chosen IMDb IDs are then looked up on Wikidata in batches (property P345) to find any entity that already exists.

Writes one YAML document per processed film to `-OutputPath`. The `imdb` section is a list of all surviving candidates ordered the same way the best match is chosen (preferred title type, then most votes, then lowest IMDb ID), so the selected candidate is first; the `wikidata` section corresponds to that selected candidate. The `imdb` and/or `wikidata` sections are omitted when no match was found. Each document ends with `verified: false`, a placeholder for a future manual-review workflow:

```yaml
netflix:
  title: <netflix-title>
  id: <netflix-id>
  url: <netflix-title-url>
imdb:
  - title: <imdb-title>
    type: <imdb-title-type>
    year: <imdb-start-year>
    id: <imdb-id>
    url: <imdb-title-url>
  # ...additional candidates in ranked order
wikidata:
  id: <wikidata-id>
  url: <wikidata-entity-url>
verified: false
---
# one document per film in YAML stream
```


## CommitWikidataIdentifiers.ps1
### Usage
```powershell
CommitWikidataIdentifiers.ps1 -CandidatesPath <candidates.yaml> [-AccessToken <token>] [-ApiUrl <url>] [-Prune] [-WhatIf]
```

### Parameters
- `-CandidatesPath` - Path to the candidates YAML stream produced by `SuggestWikidataIdentifiers.ps1` (after manual review).
- `-AccessToken` - Wikidata OAuth 2.0 owner-only access token used to authenticate edits. Falls back to the `WIKIDATA_ACCESS_TOKEN` environment variable. Only required when actually writing (not needed with `-WhatIf`).
- `-ApiUrl` - Wikidata Action API endpoint. Defaults to `https://www.wikidata.org/w/api.php`.
- `-Prune` - After processing, rewrites the candidates file with every entry that was already up to date (the target entity already had both identifiers) removed, leaving only records that still need attention. Honors `-WhatIf` (no file is written when previewing).
- `-WhatIf` - Previews the claims that would be added without making any edits (and without requiring a token).

### Behavior
Reads the candidates YAML and writes Netflix ID (P1874) and IMDb ID (P345) claims to the corresponding Wikidata entities. Only `netflix.id`, `imdb.id`, and `wikidata.id` are used; all other fields are ignored apart from the `verified` flag.

Each document is processed as follows:
- Documents without `verified: true` are ignored.
- The target entity is identified by `wikidata.id` (a `Q`-number). When a verified record has no `wikidata.id`, the script searches Wikidata for the entity carrying the record's `imdb.id` (P345) and uses that entity; the record is skipped with a warning if there is no `imdb.id` to search by, no matching entity is found, or the IMDb ID maps to more than one entity. A resolved or supplied `wikidata.id` that is not a valid `Q`-number is skipped with a warning.
- A record with more than one `imdb` entry is skipped with a warning - the reviewer is expected to remove the incorrect entries first. A record with a single `imdb` entry contributes its `imdb.id` (validated as `tt`-number); a record with no `imdb` entry commits only the Netflix ID.
- Existing P345/P1874 claims on the entity are read first (a public, unauthenticated request). If the entity already has an IMDb **or** Netflix ID that does not match the candidate, the whole record is skipped with a warning, since a mismatching identifier means the `wikidata.id` mapping is suspect.
- Only missing claims are added, so re-running the script is idempotent. A record whose identifiers are already present is reported as up to date. When `-Prune` is specified, these already-up-to-date entries are removed from the candidates file at the end of the run (skipped, non-verified, and still-pending records are preserved).

Writes use the Action API (`wbcreateclaim`) with `assert=user` and `maxlag=5` (retrying on replication lag); a CSRF token is fetched lazily only when an edit is actually performed. Per-record failures are reported individually so a partial update (e.g. Netflix written but IMDb failed) is visible and corrected on the next run.


## PopulateNatively.ps1
### Usage
```powershell
PopulateNatively.ps1 -DatabasePath <films.yaml> -NativelyDataPath <path> [-UpdateNativelyData] [-NativelyLanguage <code>] [-OriginalLanguage <codes>]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-NativelyDataPath` - Path to a local folder for storing the Natively video catalog downloaded from [Natively](https://learnnatively.com).
- `-UpdateNativelyData` - When specified, re-downloads the Natively catalog even if a local copy already exists.
- `-NativelyLanguage` - Two-letter ISO 639-1 language code for the Natively catalog (defaults to `ja`). Converted internally to the three-letter ISO 639-2 code (e.g. `ja` → `jpn`) used for the Natively API and the local data file names.
- `-OriginalLanguage` - Optional list of two-letter ISO 639-1 codes. When non-empty, only films whose `sources.wikidata.language` includes one of these codes are processed; a warning is emitted if that field is absent.

### Behavior
Pre-fetches the Natively video catalog for the language in two slices - movies (`itype=movie`, `series=all_volumes`, expanding collections into individual movie volumes) and TV series (`itype=tv_season`, `series=series`, grouping seasons under their parent series) - paging through the search API with no `q=` filter and storing them as `natively-movies-<code>.json` and `natively-tv-<code>.json` in `-NativelyDataPath` (where `<code>` is the three-letter ISO 639-2 language code). Uses the existing local files unless they are missing or `-UpdateNativelyData` is specified. Each catalog entry is normalized to a TMDB id, a kind (`movie` or `tv-series`, distinguishing movies from TV series/seasons), difficulty level, and URL, and indexed by TMDB ID + kind. When multiple catalog entries share the same key, the one with a usable level (and a non-season source) is preferred.

Matches complete `identifiers.tmdb.id`/`kind` pairs, optionally filtering
`sources.wikidata.language` with `-OriginalLanguage`. Every eligible record is
looked up each run. A usable match replaces `sources.natively` with `language`,
`level`, optional `temporaryLevel: true`, and `url`. Missing matches warn and
leave existing metadata intact. No separate Natively ID is invented. Catalog
download is skipped when no entries need lookup and no refresh was requested.

## ExportFilms.ps1
### Usage
```powershell
ExportFilms.ps1 -DatabasePath <films.yaml> [-OutputPath <films.json>]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.
- `-OutputPath` - Path to the output JSON file. Defaults to `films.json` in the current directory.

### Behavior
Exports films.yaml to a JSON file with the same structure, wrapping all film records in an outer JSON array.

## MigrateFilmsDatabase.ps1
### Usage
```powershell
MigrateFilmsDatabase.ps1 -DatabasePath <films.yaml> [-TitleLanguage <code>]
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file to migrate in place.
- `-TitleLanguage` - Two-letter ISO 639-1 language code used as the key for the old `title-ja` value in the new per-language `title` dictionary (defaults to `ja`).

### Behavior
Migrates the original V1 format to V2, converting scalar `title` / `title-ja` into
language-keyed titles and renaming `originCountry` to `country`,
`originalLanguage` to `language`, and `providers` to `availability`. Follow it
with the V2-to-V3 migration below. Already-migrated V2/V3 entries are left unchanged.

## MigrateFilmsDatabaseV2.ps1
### Usage
```powershell
MigrateFilmsDatabaseV2.ps1 -DatabasePath <films.yaml> [-MigrateCaches]
```

### Behavior
Migrates V2 records to V3, moving shared IDs to `identifiers` and metadata to
`sources`, including Wikidata's
formerly top-level descriptive fields. Copies the old IMDb title into both
`imdb` and `imdb-lists`, and the old Netflix title into both `netflix-unogs` and
`netflix-watchlist`; later imports refresh each source independently.

An old `imdb` origin becomes `imdb-lists` only if memberships remain. An old
`netflix` origin is split using field presence: `watchlist` (including false)
implies `netflix-watchlist`, any non-ID/title/watchlist field implies
`netflix-unogs`, and an ID/title-only record gets both. Source data alone does
not invent origins. Unknown top-level fields are retained, original V1 fields
require `MigrateFilmsDatabase.ps1` first, and conflicting V2/V3 values fail before
writing. Unknown availability providers also fail rather than lose data.
Migration is idempotent and rewrites the database only if records changed.

`-MigrateCaches` additionally moves inputs/caches under `sources` beside the
database. It works even if all records have already been migrated:

| Old path | New path |
| --- | --- |
| `sources/imdb/lists/` | `sources/imdb-lists/` |
| `sources/netflix/netflix-catalog-*.json` | `sources/netflix-unogs/` (same filenames) |
| `sources/netflix/netflix-my-list*.json` | `sources/netflix-watchlist/` (same filenames, including localized exports) |

IMDb datasets, TMDB/Natively caches, and unrecognized files remain in place.
Existing destinations fail preflight before any moves or database writes;
nothing is silently overwritten. Repeated runs do not move files again.

## SyncFilms.ps1
### Usage
```powershell
SyncFilms.ps1 -Path <discover-films-dir> [-Force] [-NetflixCatalog] [-NetflixRequestLimit <n>] [-IMDbExportsPath <html>] [-UpdateImdbData] [-UpdateTmdbData] [-UpdateNativelyData] [-LogPath <log>]
```

### Behavior
Optionally imports the Netflix catalog and downloads IMDb list exports, then
imports IMDb lists and the Netflix watchlist, populates Wikidata, IMDb, TMDB, and
Natively, and exports `films.json`. `-Force` refreshes Wikidata; it does not force
creation of missing watchlist entries. Cache/input paths are relative to `-Path`:

| Input | Directory / file |
| --- | --- |
| IMDb lists | `sources/imdb-lists/` |
| Netflix catalog | `sources/netflix-unogs/` |
| Netflix watchlist | `sources/netflix-watchlist/netflix-my-list.json` |
| IMDb datasets | `sources/imdb/` |
| TMDB responses | `sources/tmdb/` |
| Natively catalog | `sources/natively/` |

Use `MigrateFilmsDatabaseV2.ps1 -MigrateCaches` before syncing an old directory
layout so cached catalog details are reused instead of downloaded again.
