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
Imports Netflix watch list into films.yaml by looking up the wikidata entity ID associated with the Netflix videoId and adding or updating the corresponding entry in films.yaml, including the following fields: `wikidata`, `providers.netflix.id`, `providers.netflix.title`, and `providers.netflix.watchlist`. Additionally, any items in films.yaml with `providers.netflix.watchlist` set to `true` that are no longer in the imported watch list should be set to `false`.
