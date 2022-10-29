# Rapid API for uNoGS Netflix data

API documentation: https://rapidapi.com/unogs/api/unogs/

Get API Key from https://rapidapi.com/developer/security/unogs-v1-app

To update CSV files:
```
$env:RAPID_API_KEY = '<key>'
.\DownloadNetflixTitles.ps1
```

To query:
```
. .\NetflixQueryTools.ps1
fn <title>
```

# Netflix My List export

Chrome extension: https://chrome.google.com/webstore/detail/netflix-watch-list-manage/obgidigipndchfoaapdbldekffjpmmfa
