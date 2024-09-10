# NOTE - even when using Import-Module -Scope Local, it seems that the module is persisted in some way
# when loaded by a script and changes are not reflected without creating a new PowerShell session or using
# -Force on the Import-Module invocation (can do this temporarily to force loading changes during development)

function TestCommand {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [String]
        $Name
    )
    return !!(Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function RequireCommand {
    param(
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [String]
        $Name
    )

    if (-not (TestCommand $Name)) {
        throw "Requires $Name"
    }
}

function ArgsToArray {
    # It seems like there should be some built-in way to do this...
    # This is useful when building up a command to invoke with & operator, e.g.
    # $cmdargs = ArgsToArray /arg1 /arg2 arg2-value /arg3
    # if ($someCondition) { $cmdargs += '/arg4' }
    # & 'somecmd' $cmdargs
    return $args
}

function StringToLines($s) {
    $r = [IO.StringReader]::new($s)
    while ($true) {
        $line = $r.ReadLine()
        if ($line -eq $null) {
            return
        } else {
            $line
        }
    }
}

function GetValueByPath($target, $path) {
    $targetPath = $path.Split('.')
    for ($i = 0; $i -lt $targetPath.Count; $i++) {
        $prop = $targetPath[$i]
        if ($i -lt $targetPath.Count - 1) {
            if (-not $target.$prop) {
                return
            }
            $target = $target.$prop
        } else {
            return $target.$prop
        }
    }
}

function SetValueByPath($target, $path, $value) {
    $targetPath = $path.Split('.')
    for ($i = 0; $i -lt $targetPath.Count; $i++) {
        $prop = $targetPath[$i]
        if ($i -lt $targetPath.Count - 1) {
            if (-not $target.$prop) {
                $target.$prop = @{}
            }
            $target = $target.$prop
        } else {
            $target.$prop = $value
        }
    }
}

function WritePipelineProgress {
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        $InputObject,

        [Parameter(Mandatory)]
        [long]
        $TotalCount,

        [Parameter()]
        [String]
        $Activity = 'Processing',

        [Parameter()]
        [String]
        $ItemName = 'item'
    )
    begin {
        $itemNumber = 0
    }
    process {
        $itemNumber++
        Write-Progress -Activity $Activity -Status "$ItemName $itemNumber/$TotalCount" -PercentComplete (($itemNumber / $TotalCount) * 100)
        $InputObject
    }
    end {
        Write-Progress -Completed
    }
}

function CreateDirectoryIfNotExists([String]$Path, [Switch]$Clobber) {
    if (-not (Test-Path $Path)) {
        [void] (New-Item $Path -ItemType Directory)
    } elseif ($Clobber) {
        Remove-Item "$Path/*" -Recurse -Force
    }
}

function CopyItemIfNewer {
    param(
        [Parameter(Mandatory=$true)]
        [String[]]
        $Path,

        [Parameter(Mandatory=$true)]
        [String]
        $Destination,

        [Parameter()]
        [String]
        $Filter,

        [Parameter()]
        [String[]]
        $Include,

        [Parameter()]
        [Switch]
        $Force,

        [Parameter()]
        [Switch]
        $PassThru
    )

    Get-ChildItem $Path -Filter $Filter -Include $Include |
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

function ReplaceStringInFiles {
    param(
        [Parameter(Mandatory=$true,Position=0)]
        [String]
        $Search,

        [Parameter(Mandatory=$true,Position=1)]
        [AllowEmptyString()]
        [String]
        $Replace,

        [Parameter(ParameterSetName='inputObject',Mandatory=$true,ValueFromPipeline=$true,Position=2)]
        [PSObject]
        $InputObject,

        [Parameter(ParameterSetName='path',Mandatory=$true,Position=2)]
        [String[]]
        $Path
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'inputObject') {
            $searchList = $InputObject | Select-String -Pattern $Search -List
        } elseif ($PSCmdlet.ParameterSetName -eq 'path') {
            $searchList = Select-String -Pattern $Search -Path $Path -List
        }
        $searchList | Foreach-Object {
            (Get-Content $_.Path -Raw) -replace $Search,$Replace | Set-Content $_.Path }
    }
}

function CompressArchive([String[]]$Path, [String]$DestinationPath, [Switch]$Force) {
    # Ensure destination directory exists (7z would do this automatically but Compress-Archive does not)
    $destinationDir = Split-Path $DestinationPath -Parent
    CreateDirectoryIfNotExists $destinationDir

    # Use 7z if available for performance reasons (Compress-Archive can be very slow)
    if (TestCommand 7z) {
        if ($Force -and (Test-Path $DestinationPath)) {
            Remove-Item $DestinationPath
        }
        # Normalize destination path before passing to 7z
        # NOTE - not normalizing $Path currently because it could have wildcards
        # (both Resolve-Path and Convert-Path expand wildcards)
        $DestinationPath = Join-Path (Resolve-Path $destinationDir) (Split-Path $DestinationPath -Leaf)
        7z a -tzip -r $DestinationPath $Path
    } else {
        Compress-Archive -Path $Path -DestinationPath $DestinationPath -Force:$Force
    }
}

function ExpandArchive([String]$Path, [String]$DestinationPath, [Switch]$ClobberDestination) {
    if ($ClobberDestination -and (Test-Path $DestinationPath)) {
        Remove-Item $DestinationPath -Recurse -Force
    }

    # Use 7z if available for performance reasons
    if (TestCommand 7z) {
        # NOTE - not currently normalizing either of these paths (but could if needed)
        7z x -o"$DestinationPath" $Path
    } else {
        Expand-Archive -Path $Path -DestinationPath $DestinationPath
    }
}

function GetRelativePath([string]$basePath, [string]$path) {
    return [IO.Path]::GetRelativePath($basePath, $path).Replace('\', '/')
}

function GetImageExtensions {
    return @('.jpg','.jpeg','.png')
}

function GetKnownAudioExtensions([Switch]$FileMask) {
    $prefix = $FileMask ? '*' : ''
    return @("$prefix.mp3","$prefix.m4a")
}

function GetContentBaseName($contentPath) {
    # Strip file-type extension (.yaml/.md)
    $base = Split-Path $contentPath -LeafBase
    if ($base -eq 'content') {
        return $null
    } elseif ((Split-Path $base -Extension) -eq '.content') {
        return (Split-Path $base -LeafBase)
    }
    return $base
}

function InvokeTandokuCommand {
    $tandokuArgs = ($args.Length -eq 1 -and $args[0] -is [Array]) ? $args[0] : $args

    $tandokuArgs += '--json-output'

    $tandokuOut = (& 'tandoku' $tandokuArgs) | Out-String
    if ($tandokuOut) {
        # NOTE: not using ConvertFrom-Json -AsHashtable as this returns a *case-sensitive* hashtable as of PowerShell 7.3
        # see also https://github.com/PowerShell/PowerShell/issues/19928
        return (ConvertFrom-Json $tandokuOut)
    } else {
        # TODO: capture error output? (or just let it go to console?)
        return
    }
}

Export-ModuleMember -Function *
