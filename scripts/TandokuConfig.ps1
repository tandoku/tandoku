using namespace System.Management.Automation

param(
    [Parameter()]
    [ArgumentCompleter({
        param($Command, $Parameter, $WordToComplete, $CommandAst, $FakeBoundParams)

        $allKeyPatterns = ([ValidKeysGenerator]::new()).GetValidValues()
        $matching = @()
        foreach ($keyPattern in $allKeyPatterns) {
            if ($keyPattern.EndsWith('*')) {
                $keyPattern = $keyPattern.Substring(0, $keyPattern.Length - 1)
            }
            if ($keyPattern -like "$WordToComplete*") {
                $matching += $keyPattern
            }
        }
        return $matching
    })]
    [ValidateScript({
        $allKeyPatterns = ([ValidKeysGenerator]::new()).GetValidValues()
        foreach ($keyPattern in $allKeyPatterns) {
            if ($keyPattern.EndsWith('*')) {
                if ($_ -like $keyPattern) {
                    return $true
                }
            } else {
                if ($_ -eq $keyPattern) {
                    return $true
                }
            }
        }
        return $matching
    })]
    [String]
    $Key,

    [Parameter()]
    $Value,

    [Parameter()]
    [ValidateSet('user','volume')] # TODO - library
    [String]
    $Scope = 'user',

    [Parameter()]
    $Volume
)

class ValidKeysGenerator : IValidateSetValuesGenerator {
    [string[]] GetValidValues() {
        # TODO - register/collect all workflow-params and other keys (some kind of parameters
        # exported by scripts) so that the IValidateSetValuesGenerator implementation can provide
        # the full set of keys instead of * patterns.
        # ArgumentCompleter and ValidateScript can then be replaced with ValidateSet.
        # see https://vexx32.github.io/2018/11/29/Dynamic-ValidateSet/
        return @(
            'azure-computer-vision.apikey',
            'azure-computer-vision.endpoint',
            'core.staging',
            'epub.staging',
            'workflow-params.*'
        )
    }
}

Import-Module "$PSScriptRoot/modules/tandoku-utils.psm1" -Scope Local
Import-Module "$PSScriptRoot/modules/tandoku-volume.psm1" -Scope Local

switch ($Scope) {
    'volume' {
        $Volume = ResolveVolume $Volume
        if (-not $Volume) {
            return
        }
        $volumePath = $Volume.Path
        # TODO - add MetadataDirectoryPath to volume info
        # and probably eventually ConfigPath as well
        $configPath = "$volumePath/.tandoku-volume/config.yaml"
    }
    'user' {
        $configPath = '~/.tandoku/config.yaml'
    }
}

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