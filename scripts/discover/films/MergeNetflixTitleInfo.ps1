$lang = 'ja'

function MergeNetflixTitles {
    $sub = Import-Csv ".\netflix-$lang-subs+audio.csv"
    $all = Import-Csv ".\netflix-$lang-audio.csv"

    $sanityCheck = $sub|?{$all.netflixid -notcontains $_.netflixid}|count
    if ($sanityCheck -gt 0) {
        Write-Warning "$sanityCheck titles in subs list are missing in complete list"
    }

    $all | Foreach-Object {
        $sublang = ($sub.netflixid -contains $_.netflixid) ? $lang : $null
        $_ | Add-Member -NotePropertyName audio -NotePropertyValue $lang -PassThru |
            Add-Member -NotePropertyName subtitles -NotePropertyValue $sublang -PassThru
    }
}

function AddMyListIndicator {
    $mylist = Get-Content .\netflix-my-list.json|ConvertFrom-Json
    $input | Foreach-Object {
        $inmylist = ($mylist.videoId -contains $_.netflixid)
        $_ | Add-Member -NotePropertyName mylist -NotePropertyValue $inmylist -PassThru
    }
}

function AddIMDbRating {
    $ratings = Import-Csv .\imdb\title.ratings.tsv -Delimiter `t |
        Group-Object tconst -AsHashTable

    $input | Foreach-Object {
        $r = $ratings[$_.imdbid]
        $avgRating = [double]$r.averageRating
        $numVotes = [int]$r.numVotes
        $_ | Add-Member -NotePropertyName imdbRating -NotePropertyValue $avgRating -PassThru |
            Add-Member -NotePropertyName imdbNumVotes -NotePropertyValue $numVotes -PassThru
    }
}

MergeNetflixTitles |
    AddMyListIndicator |
    AddIMDbRating |
    Export-Csv ".\netflix-$lang-titles.csv"
