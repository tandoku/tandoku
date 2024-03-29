# NOTE - even when using Import-Module -Scope Local, it seems that the module is persisted in some way
# when loaded by a script and changes are not reflected without creating a new PowerShell session or using
# -Force on the Import-Module invocation (can do this temporarily to force loading changes during development)

function ArgsToArray {
    # It seems like there should be some built-in way to do this...
    # This is useful when building up a command to invoke with & operator, e.g.
    # $cmdargs = ArgsToArray /arg1 /arg2 arg2-value /arg3
    # if ($someCondition) { $cmdargs += '/arg4' }
    # & 'somecmd' $cmdargs
    return $args
}

function CreateDirectoryIfNotExists([String] $Path) {
    if (-not (Test-Path $Path)) {
        [void] (New-Item $Path -ItemType Directory)
    }
}

# TODO - replace this with [IO.Path]::GetRelativePath
function ExtractRelativePath([String] $basePath, [String] $childPath) {
    if ($basePath -eq $childPath) {
        return '.'
    }

    $basePathWithSep = Join-Path $basePath /
    $childPathWithSep = Join-Path $childPath /
    if ($basePathWithSep -eq $childPathWithSep) {
        return '.'
    }

    if ($childPath -like "$basePathWithSep*") {
        return $childPath.Substring($basePathWithSep.length)
    } else {
        return $null
    }
}

# TODO - drop support for mapping to PSDrives
function MapToPSDriveAlias {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullorEmpty()]
        [String]
        $Path
    )

    $drives = Get-PSDrive -PSProvider FileSystem |
        Sort-Object -Property Root -Descending

    foreach ($drive in $drives) {
        $relativePath = ExtractRelativePath $drive.Root $path
        if ($relativePath) {
            return (Join-Path "$($drive.Name):/" $relativePath -Resolve)
        }
    }

    return $path
}

function InvokeTandokuCommand {
    $tandokuArgs = ($args.Length -eq 1 -and $args[0] -is [Array]) ? $args[0] : $args

    $tandokuArgs += '--json-output'

    $tandokuOut = (& 'tandoku' $tandokuArgs) | Out-String
    if ($tandokuOut) {
        return (ConvertFrom-Json $tandokuOut)
    } else {
        # TODO: capture error output? (or just let it go to console?)
        return
    }
}

Export-ModuleMember -Function *
