[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ImdbDataPath,

    [Parameter()]
    [string[]]$Datasets = @('title.ratings'),

    [switch]$UpdateImdbData
)

# Downloads the requested IMDb datasets (https://datasets.imdbws.com) into $ImdbDataPath,
# extracting each `<dataset>.tsv.gz` to `<dataset>.tsv`. Returns an ordered hashtable mapping
# each dataset name to the path of its extracted .tsv file. Existing .tsv files are reused
# unless -UpdateImdbData is specified.

if (-not (Test-Path $ImdbDataPath)) {
    New-Item -ItemType Directory -Path $ImdbDataPath | Out-Null
}

$ImdbDataPath = (Resolve-Path $ImdbDataPath).Path

$result = [ordered]@{}
foreach ($dataset in $Datasets) {
    $gzPath = Join-Path $ImdbDataPath "$dataset.tsv.gz"
    $tsvPath = Join-Path $ImdbDataPath "$dataset.tsv"

    if ($UpdateImdbData -or -not (Test-Path $tsvPath)) {
        if ($UpdateImdbData -or -not (Test-Path $gzPath)) {
            $url = "https://datasets.imdbws.com/$dataset.tsv.gz"
            Write-Host "Downloading $url..."
            Invoke-WebRequest -Uri $url -OutFile $gzPath
        }

        Write-Host "Extracting $dataset.tsv..."
        # Extract to a temporary file first so an interrupted run never leaves a partial .tsv
        $tmpPath = "$tsvPath.tmp"
        $gzIn = [System.IO.File]::OpenRead($gzPath)
        $gzStream = [System.IO.Compression.GZipStream]::new($gzIn, [System.IO.Compression.CompressionMode]::Decompress)
        $tsvOut = [System.IO.File]::Create($tmpPath)
        $gzStream.CopyTo($tsvOut)
        $tsvOut.Close()
        $gzStream.Close()
        $gzIn.Close()
        Move-Item -LiteralPath $tmpPath -Destination $tsvPath -Force
    }

    $result[$dataset] = $tsvPath
}

return $result
