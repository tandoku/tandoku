# discover films
Tools for compiling a database to aid discovery and ranking of Japanese films (including movies and TV series) for language learning.

# Data
## `films.yaml`
YAML file containing a stream of documets representing films (movie or TV series).

```yaml
wikidata: Q130305455
providers:
  netflix:
    id: 82024665
    title: The Fragrant Flower Blooms With Dignity
    watchlist: true
---
wikidata: Q626942
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
Iterates over each entry in films.yaml that is missing the `wikidata` field. For entries that have `providers.netflix.id`, looks up the wikidata entity ID associated with the Netflix ID (using Wikidata property P1874) and updates the `wikidata` field.
