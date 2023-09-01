param(
    [Parameter(Mandatory=$true)]
    [String[]]
    $Path,

    # TODO: add 'auto' value, only allowed if $Path already exists, checks for $Path.dvc file to determine if binary
    [Parameter(Mandatory=$true)]
    [ValidateSet('text', 'binary', 'ignore')]
    [String]
    $Kind
)

switch ($Kind) {
    'text' { git add $Path }
    'binary' { dvc add $Path }
}