param(
    [Parameter(Mandatory=$true)]
    [String[]]
    $Path,

    [Parameter(Mandatory=$true)]
    [ValidateSet('text', 'binary', 'ignore')]
    [String]
    $Kind
)

switch ($Kind) {
    'text' { git add $Path }
    'binary' { dvc add $Path }
}