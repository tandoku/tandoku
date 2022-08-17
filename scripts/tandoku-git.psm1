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
