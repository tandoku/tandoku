param(
    [Parameter(Mandatory=$true)]
    [String[]]
    $Path,

    [Parameter(Mandatory=$true)]
    [ValidateSet('text', 'binary', 'auto', 'ignore')]
    [String]
    $Kind
)

# Chunk adds to avoid errors when adding excessively large number of files
$chunkSize = 200
for ($i = 0; $i -lt $Path.Count; $i += $chunkSize) {
    $items = $Path[$i..($i + $chunkSize - 1)]

    switch ($Kind) {
        'text' { git add $items }
        'binary' { dvc add $items }
        'auto' {
            # TODO: check that each item already exists, check for <item>.dvc file to determine if binary (***for directories as well***)
            # >>> if item doesn't exist, must assume it (was) a directory for TandokuVolumeRename to work !!
            if ($items.Count -eq 1 -and (Test-Path $items -PathType Container)) {
                # Note: this assumes any binary files under directory have already been added individually
                # This should only be used to handle directory renames
            } else {
                Write-Warning "TODO: auto for multiple/non-directory paths"
            }
            git add $items
        }
    }
}