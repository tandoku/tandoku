#Requires -Version 7

<#
.SYNOPSIS
    Deletes bin/ and obj/ directories for each project in tandoku.slnx.

.DESCRIPTION
    Fixes spurious dotnet build failures caused by stale build outputs, such as:
        error CS0579: Duplicate 'global::System.Runtime.Versioning.TargetFrameworkAttribute' attribute

    Reads project paths from the solution file and removes bin/ and obj/ only
    under those project directories.

.PARAMETER SolutionPath
    Path to the .slnx solution file. Defaults to tandoku.slnx next to this script.

.EXAMPLE
    ./PurgeDotnetBuild.ps1
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $SolutionPath = (Join-Path $PSScriptRoot 'tandoku.slnx')
)

$ErrorActionPreference = 'Stop'

$solutionFile = (Resolve-Path -LiteralPath $SolutionPath).Path
$solutionDir = Split-Path -Parent $solutionFile

Write-Host "Reading projects from $solutionFile" -ForegroundColor Cyan

[xml] $slnx = Get-Content -LiteralPath $solutionFile -Raw
$projectDirs = $slnx.Solution.Project |
    ForEach-Object { Split-Path -Parent (Join-Path $solutionDir $_.Path) } |
    Sort-Object -Unique

$targets = foreach ($dir in $projectDirs) {
    foreach ($name in 'bin', 'obj') {
        $candidate = Join-Path $dir $name
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            Get-Item -LiteralPath $candidate
        }
    }
}

if (-not $targets) {
    Write-Host "Nothing to remove." -ForegroundColor Green
    return
}

foreach ($dir in $targets) {
    if ($PSCmdlet.ShouldProcess($dir.FullName, 'Remove-Item -Recurse -Force')) {
        Write-Host "Removing $($dir.FullName)"
        Remove-Item -LiteralPath $dir.FullName -Recurse -Force
    }
}

Write-Host "Done." -ForegroundColor Green
