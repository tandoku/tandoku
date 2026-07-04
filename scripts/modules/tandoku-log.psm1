# Shared logging support for the tandoku scripts.
#
# Scripts opt in by importing this module and calling Initialize-TandokuLog with
# their -LogPath parameter. When a (non-empty) LogPath is supplied, warnings and
# errors are additionally appended to that file (in addition to the console);
# when LogPath is empty the proxies become pass-throughs and logging is disabled.
#
# The proxy Write-Warning / Write-Error functions exported here shadow the
# built-in cmdlets in the importing script, so existing Write-Warning /
# Write-Error calls are captured without any further changes. To also capture
# uncaught terminating errors (throw), a script should add a trap that forwards
# to Write-TandokuLogEntry, e.g.:
#
#     trap { Write-TandokuLogEntry 'ERROR' $_; break }
#
# NOTE: scripts invoked with the & operator share a PowerShell session (as the
# discover/films SyncFilms.ps1 does), so this module's state is shared across
# them. Each script calls Initialize-TandokuLog at startup, which resets the
# active log path for that script's run.

$script:LogPath = $null

# Configures the active log file for subsequent Write-Warning / Write-Error /
# Write-TandokuLogEntry calls. Pass an empty/null LogPath to disable logging.
# Creates the log file's parent directory if it does not already exist.
function Initialize-TandokuLog {
    param(
        [Parameter()]
        [AllowEmptyString()]
        [AllowNull()]
        [string]$LogPath
    )

    $script:LogPath = $LogPath
    if ($script:LogPath) {
        $logDir = Split-Path -Parent $script:LogPath
        if ($logDir -and -not (Test-Path -LiteralPath $logDir)) {
            New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        }
    }
}

# Appends a single timestamped entry to the active log file. No-op when logging
# is disabled (no LogPath configured).
function Write-TandokuLogEntry {
    param(
        [Parameter(Mandatory)]
        [string]$Level,

        [Parameter(Mandatory, ValueFromPipeline)]
        [string]$Message
    )
    process {
        if ($script:LogPath) {
            Add-Content -LiteralPath $script:LogPath -Value "$(Get-Date -Format s) $Level $Message"
        }
    }
}

# Proxy for Write-Warning that additionally records the warning to the active log
# file, then forwards to the real cmdlet so console output is unchanged.
function Write-Warning {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string]$Message
    )
    process {
        Write-TandokuLogEntry 'WARNING' $Message
        Microsoft.PowerShell.Utility\Write-Warning $Message
    }
}

# Proxy for Write-Error that additionally records the error to the active log
# file, then forwards to the real cmdlet so console output / error stream
# behavior is unchanged.
function Write-Error {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string]$Message
    )
    process {
        Write-TandokuLogEntry 'ERROR' $Message
        Microsoft.PowerShell.Utility\Write-Error $Message
    }
}

Export-ModuleMember -Function Initialize-TandokuLog, Write-TandokuLogEntry, Write-Warning, Write-Error
