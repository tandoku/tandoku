$env:RAPID_API_KEY = 'e4433ea583msh9ba1e9a6d336fccp1c7dedjsn1349cc02e883'
.\GetNetflixTitles.ps1 -AudioOnly |export-csv '.\netflix-ja-audio.csv'
.\GetNetflixTitles.ps1 |export-csv '.\netflix-ja-subs+audio.csv'
