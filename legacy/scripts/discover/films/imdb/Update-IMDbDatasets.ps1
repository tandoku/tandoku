param(
    [Parameter()]
    [Switch]
    $Force,

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
    'title.ratings') | %{ "$_.tsv" }
foreach ($f in $files) {
    $gzf = "$f.gz"
    if ($Force -or (-not (Test-Path $f) -and -not (Test-Path $gzf))) {
        Write-Host "Downloading $gzf"
        Invoke-WebRequest "https://datasets.imdbws.com/$gzf" -OutFile $gzf
    }
}

if ($Unpack) {
    foreach ($f in $files) {
        $gzf = "$f.gz"
        if ((Test-Path $gzf) -and ($Force -or -not (Test-Path $f))) {
            Write-Host "Unpacking $gzf"
            7z x $gzf
            if (Test-Path $f) { Remove-Item $f }
            Move-Item data.tsv $f
            Remove-Item $gzf
        }
    }
}
