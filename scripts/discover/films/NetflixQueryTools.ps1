function AddIMDbTitleUrl {
    process {
        $input | Foreach-Object {
            $id = $_.imdbid
            $url = ($id -and $id -ne 'notfound') ? "https://www.imdb.com/title/$id/" : $null
            $_|Add-Member -NotePropertyName imdburl -NotePropertyValue $url -PassThru
        }
    }
}

$netflixSubTitles = Import-Csv '.\netflix-ja-subs+audio.csv'|AddIMDbTitleUrl
$netflixAllTitles = Import-Csv '.\netflix-ja-audio.csv'|AddIMDbTitleUrl

$sanityCheck = $netflixSubTitles|?{$netflixAllTitles.netflixid -notcontains $_.netflixid}|count
if ($sanityCheck -gt 0) {
    Write-Warning "$sanityCheck titles in subs list are missing in complete list"
}

function FindNetflixTitle($title) {
    $netflixSubTitles|? title -match $title

    $alt = $netflixAllTitles|? title -match $title|?{$netflixSubTitles.netflixid -notcontains $_.netflixid}
    if ($alt) {
        Write-Warning "The following title(s) are audio-only"
        $alt
    }
}
Set-Alias fn FindNetflixTitle

$netflixMyTitles = Get-Content .\netflix-my-list.json|ConvertFrom-Json |
    ForEach-Object {
        $jaAudio = $netflixAllTitles.netflixid -contains $_.videoId
        $jaSubs = $netflixSubTitles.netflixid -contains $_.videoId
        $_ | Add-Member -NotePropertyName jaAudio -NotePropertyValue $jaAudio -PassThru |
            Add-Member -NotePropertyName jaSubs -NotePropertyValue $jaSubs -PassThru }
