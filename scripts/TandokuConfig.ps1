param(
    [Parameter()]
    [ValidateSet('azure-computer-vision.apiKey','azure-computer-vision.endpoint')] # TODO - get this from some central location (generate from config exported by scripts)
    [String]
    $Key,

    [Parameter()]
    $Value
)

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local

$configPath = '~/.tandoku/config.yaml'
$config = (Test-Path $configPath) ?
    (Get-Content $configPath | ConvertFrom-Yaml) :
    @{}

if ($Key) {
    # Check $PSBoundParameters explicitly since $Value could be null/zero/empty string
    if ($PSBoundParameters.ContainsKey('Value')) {
        SetValueByPath $config $Key $Value
        CreateDirectoryIfNotExists (Split-Path $configPath -Parent)
        $config | ConvertTo-Yaml | Set-Content $configPath
    } else {
        return GetValueByPath $config $Key
    }
} else {
    return $config
}