param(
    [Parameter()]
    [Switch]
    $Unpack
)

$files = @(
    'name.basics',
    'title.akas',
    'title.basics',
    'title.crew',
    'title.episode',
    'title.principals',
    'title.ratings') | %{ "$_.tsv.gz" }
foreach ($f in $files) {
    Write-Host "Downloading $f"
    Invoke-WebRequest "https://datasets.imdbws.com/$f" -OutFile $f
}

if ($Unpack) {
    foreach ($f in $files) {
        Write-Host "Unpacking $f"
        7z x $f
        [void] $f -match '(.+)\.gz'
        Move-Item data.tsv $matches[1]
    }
}
