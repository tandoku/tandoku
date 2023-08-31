function ArgsToArray {
    # It seems like there should be some built-in way to do this...
    # This is useful when building up a command to invoke with & operator, e.g.
    # $cmdargs = ArgsToArray /arg1 /arg2 arg2-value /arg3
    # if ($someCondition) { $cmdargs += '/arg4' }
    # & 'somecmd' $cmdargs
    return $args
}

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
