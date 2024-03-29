function ArgsToArray {
    # It seems like there should be some built-in way to do this...
    # This is useful when building up a command to invoke with & operator, e.g.
    # $cmdargs = ArgsToArray /arg1 /arg2 arg2-value /arg3
    # if ($someCondition) { $cmdargs += '/arg4' }
    # & 'somecmd' $cmdargs
    return $args
}

function CleanInvalidPathChars([String] $name, [String] $replaceWith = '_') {
    ($name.Split([IO.Path]::GetInvalidFileNameChars()) -join $replaceWith).Trim()
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

function CreateDirectoryIfNotExists([String] $path) {
    if (-not (Test-Path $path)) {
        [void](New-Item $path -ItemType Directory)
    }
}

function Copy-ItemIfNewer {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Path,

        [Parameter(Mandatory=$true)]
        [String]
        $Destination,

        [Parameter()]
        [Switch]
        $Force,

        [Parameter()]
        [Switch]
        $PassThru
    )

    Get-ChildItem $Path |
        Foreach-Object {
            # NOTE: this won't handle $Destination with wildcards
            if (Test-Path $Destination -PathType Container) {
                $targetPath = Join-Path $Destination (Split-Path $_ -Leaf)
            } else {
                $targetPath = $Destination
            }
            $target = (Test-Path $targetPath) ? (Get-Item -LiteralPath $targetPath) : $null
            if (-not $target -or ($target.LastWriteTime -lt $_.LastWriteTime)) {
                Copy-Item -LiteralPath $_ -Destination $Destination -Force:$Force -PassThru:$PassThru
            }
        }
}

function Test-Command {
    param(
        [Parameter(Mandatory=$true)]
        $Name
    )
    return !!(Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function New-TempDirectory {
    param(
        [Parameter()]
        [String]
        $Prefix
    )

    $dirName = New-Guid
    if ($Prefix) {
        $dirName = "$Prefix-$dirName"
    }
    $tempDirPath = Join-Path ([IO.Path]::GetTempPath()) $dirName
    [void] (New-Item $tempDirPath -ItemType Directory)

    return [TempDirectoryDisposable]::new($tempDirPath)
}

function Remove-TempDirectories {
    param(
        [Parameter(Mandatory=$true)]
        [String]
        $Prefix
    )

    $tempDirPath = Join-Path ([IO.Path]::GetTempPath()) "$Prefix-*"
    Remove-Item $tempDirPath -Recurse -Force
}

class TempDirectoryDisposable : System.IDisposable {
    [String] $TempDirPath

    TempDirectoryDisposable([string] $tempDirPath) {
        $this.TempDirPath = $tempDirPath
    }

    [void] Dispose() {
        Remove-Item $this.tempDirPath -Recurse -Force
    }
}

function ReplaceFilesViaCommand([ScriptBlock]$cmd, $files) {
    $tempDir = New-TempDirectory 'tandoku'
    $tempPath = $tempDir.TempDirPath
    try {
        foreach ($file in $files) {
            $fileName = Split-Path $file -Leaf
            $target = Join-Path $tempPath $fileName
            & $cmd -Source $file -Target $target
            if (Test-Path $target) {
                Remove-Item $file
                Move-Item $target $file
            } else {
                Write-Warning "Skipping $file because target is missing: $target"
            }
        }
    }
    finally {
        $tempDir.Dispose()
    }
}

# Consider choosing another name for Sort-STNumerical since Sort is a reserved verb
# (or try to adapt this so it is used as an argument to Sort-Object rather than doing the sorting itself)
# Note: I added the call to .Normalize([Text.NormalizationForm]::FormKC) in order to handle full-width numbers
# (which Convert.ToInt32 doesn't handle)
# adapted from https://www.powershelladmin.com/wiki/Sort_strings_with_numbers_more_humanely_in_PowerShell
function Sort-STNumerical {
    <#
        .SYNOPSIS
            Sort a collection of strings containing numbers, or a mix of this and 
            numerical data types - in a human-friendly way.

            This will sort "anything" you throw at it correctly.

            Author: Joakim Borger Svendsen, Copyright 2019-present, Svendsen Tech.

            MIT License

        .PARAMETER InputObject
            Collection to sort.

        .PARAMETER MaximumDigitCount
            Maximum numbers of digits to account for in a row, in order for them to be sorted
            correctly. Default: 100. This is the .NET framework maximum as of 2019-05-09.
            For IPv4 addresses "3" is sufficient, but "overdoing" does no or little harm. It might
            eat some more resources, which can matter on really huge files/data sets.

        .EXAMPLE
            $Strings | Sort-STNumerical

            Sort strings containing numbers in a way that magically makes them sorted human-friendly
            
        .EXAMPLE
            $Result = Sort-STNumerical -InputObject $Numbers
            $Result

            Sort numbers in a human-friendly way.
    #>
    [CmdletBinding()]
    Param(
        [Parameter(
            Mandatory = $True,
            ValueFromPipeline = $True,
            ValueFromPipelineBypropertyName = $True)]
        [System.Object[]]
        $InputObject,
        
        [ValidateRange(2, 100)]
        [Byte]
        $MaximumDigitCount = 10)
    
    Begin {
        [System.Object[]] $InnerInputObject = @()
    }
    
    Process {
        $InnerInputObject += $InputObject
    }

    End {
        $InnerInputObject |
            Sort-Object -Property `
                @{ Expression = {
                    [Regex]::Replace($_, '(\d+)', {
                        "{0:D$MaximumDigitCount}" -f [Int64] $Args[0].Value.Normalize([Text.NormalizationForm]::FormKC) })
                    }
                },
                @{ Expression = { $_ } }
    }
}

Export-ModuleMember -Function * -Alias *
