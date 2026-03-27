# discover films
Tools for compiling a database to aid discovery and ranking of Japanese films (including movies and TV series) for language learning.

# Data
## `films.yaml`
YAML file containing a stream of documets representing films (movie or TV series).

```yaml
wikidata: Q130305455
title: The Fragrant Flower Blooms with Dignity
title-ja: 薫る花は凛と咲く
type: anime-television-series
originCountry: Japan
originalLanguage: ja
year: 2025 # derived from wikidata 'start time'
imdb:
  id: tt36592690
  rating: 8.3
  votes: 12000
myAnimeList:
  id: 59845
tmdb:
  id: 271607
  kind: tv-series
natively:
  level: 21
  url: https://learnnatively.com/tv/66797d747a/
providers:
  netflix:
    id: 82024665
    title: The Fragrant Flower Blooms With Dignity
    watchlist: true
---
wikidata: Q626942
title: Good Luck!!
title-ja: GOOD LUCK!!
type: japanese-television-drama
originCountry: Japan
originalLanguage: ja
year: 2003
imdb:
  id: tt0399971
  rating: 7.5
  votes: 830
tmdb:
  id: 2146
  kind: tv-series
natively:
  level: 30
  temporaryLevel: true
  url: https://learnnatively.com/tv/8c14b5b860/
providers:
  netflix:
    id: 81922646
    title: Good Luck!!
    watchlist: true
```

# Scripts
## ImportNetflixWatchlist.ps1
### Usage
```powershell
ImportNetflixWatchlist.ps1 -Path <netflix-my-list.json> -DatabasePath <films.yaml>
```

### Parameters
- `-Path` - Path to the netflix-my-list.json created by [Netflix Watch List Manager](https://chromewebstore.google.com/detail/netflix-watch-list-manage/obgidigipndchfoaapdbldekffjpmmfa).
- `-DatabasePath` - Path to the films.yaml database file.

### Behavior
Imports Netflix watch list into films.yaml by adding or updating entries matched by `providers.netflix.id`, including the following fields: `providers.netflix.id`, `providers.netflix.title`, and `providers.netflix.watchlist`. Additionally, any items in films.yaml with `providers.netflix.watchlist` set to `true` that are no longer in the imported watch list should be set to `false`.

## PopulateWikidata.ps1
### Usage
```powershell
PopulateWikidata.ps1 -DatabasePath <films.yaml>
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.

### Behavior
Iterates over each entry in films.yaml and populates data from Wikidata in two phases:

1. **QID lookup** - For entries missing the `wikidata` field that have `providers.netflix.id`, looks up the Wikidata entity ID associated with the Netflix ID (using Wikidata property P1874).
2. **Details enrichment** - For entries that have a `wikidata` QID but are missing a `title`, queries Wikidata to populate additional fields: `title` (English label), `title-ja` (Japanese label), `type` (instance of / P31, kebab-cased), `originCountry` (country of origin / P495), `originalLanguage` (original language code / P364 + P424), `year` (start time / P580, or publication date / P577), `imdb.id` (P345), `myAnimeList.id` (P4086), `tmdb.id` (TMDb movie P4947 or TV series P4983), and `tmdb.kind` (`movie` or `tv-series` to disambiguate the TMDb ID).

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
Downloads `title.ratings.tsv.gz` from IMDb daily data dumps and extracts it to the folder specified by `-ImdbDataPath`. Uses existing data files at that path unless `-UpdateImdbData` is specified. For each entry in films.yaml that has `imdb.id`, looks up the IMDb rating and vote count and updates the `imdb.rating` and `imdb.votes` fields.

## PopulateNatively.ps1
### Usage
```powershell
PopulateNatively.ps1 -DatabasePath <films.yaml>
```

### Parameters
- `-DatabasePath` - Path to the films.yaml database file.

### Behavior
For each entry in films.yaml with `originalLanguage` of `ja` that has `title-ja` and `tmdb.id` but is missing `natively`, searches [Natively](https://learnnatively.com) for the Japanese title and matches results by TMDB ID. If a match is found, populates `natively.level` (difficulty level), `natively.url`, and `natively.temporaryLevel` (set to `true` when the level is a temporary rating). Warns if no matching result is found. Sleeps 1-2 seconds between requests to avoid overwhelming the site.

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
