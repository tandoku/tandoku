param(
    [Parameter(Mandatory=$true)]
    [String[]]
    $Path,

    [Parameter(Mandatory=$true)]
    [ValidateSet('text', 'binary', 'auto', 'ignore')]
    [String]
    $Kind
)

switch ($Kind) {
    'text' { git add $Path }
    'binary' { dvc add $Path }
    'auto' {
        # TODO: check that each $Path already exists, check for $Path.dvc file to determine if binary (***for directories as well***)
        if ($Path.Count -eq 1 -and (Test-Path $Path -PathType Container)) {
            # Note: this assumes any binary files under directory have already been added individually
            # This should only be used to handle directory renames
            git add $Path
        } else {
            Write-Error "Not implemented: auto for multiple/non-directory paths"
        }
    }
}