# TODO: this is a huge file, Import-Csv is extremely slow
$akas = Import-Csv ..\imdb\title.akas.tsv -Delimiter `t |
    Group-Object title -AsHashTable

$input |
    Foreach-Object {
        
    }