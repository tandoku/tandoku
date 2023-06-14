# TODO: refactor this to just Add-TandokuPathToSourceControl, move Complete-SourceControlChange call to New-TandokuVolume
# (leave responsibility for files to calling code - this module should manage source control only and not know specifics about files/content)
# Also, add IsSourceControlEnabled function to tandoku-core which can optionally be called before calling multiple *SourceControl cmdlets
function Add-TandokuVolumeToSourceControl {
    param(
        [Parameter()]
        [PSCustomObject]
        $Volume,

        [Parameter()]
        [Switch]
        $Commit
    )

    if (-not (CheckGitEnabled)) {
        return
    }

    $path = $Volume.Path
    $moniker = $Volume.Moniker

    git add $path

    if ($Commit) {
        Commit-SourceControlChange -Message "$moniker new volume"
    }
}

# TODO: rename to Complete-SourceControlChange
function Commit-SourceControlChange {
    param(
        [Parameter()]
        [String]
        $Message
    )

    if (-not (CheckGitEnabled)) {
        return
    }

    git commit -m $Message
}

function CheckGitEnabled {
    return ((Get-TandokuSourceControl) -eq 'git')
}

function CheckGitStatusClean {
    if (-not (IsGitStatusClean)) {
        Write-Error "Cannot process source control because git is not clean"
        return $false
    }
    return $true
}

function IsGitStatusClean {
    return ((git status -s|Out-String).Length -eq 0)
}

Export-ModuleMember -Function *-* -Alias *
