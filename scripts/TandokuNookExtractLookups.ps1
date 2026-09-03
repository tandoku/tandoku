#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter()]
    [String]
    $BackupDirectory = '~/.tandoku/nook/backup',

    [Parameter()]
    [String]
    $OutputDirectory = '~/.tandoku/nook/import',

    [Parameter()]
    [String]
    $DeviceUdid,

    [Parameter()]
    [ValidatePattern('^[A-Za-z0-9.-]+$')]
    [String]
    $BundleId = 'com.barnesandnoble.B-N-eReader',

    [Parameter()]
    [Switch]
    $SkipBackup,

    [Parameter()]
    [Switch]
    $FullBackup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$BackupDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($BackupDirectory)
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)

function GetRequiredCommand([String] $Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Required command '$Name' was not found in PATH."
    }
    return $command.Source
}

function InvokeNativeCommand([String] $Command, [String[]] $Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$Command' exited with code $LASTEXITCODE."
    }
}

function ExportSqliteCsv(
    [String] $Sqlite,
    [String] $DatabasePath,
    [String] $Query,
    [String] $OutputPath,
    [String] $Description
) {
    $csvLines = @(& $Sqlite -header -csv $DatabasePath $Query)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not export $Description from '$DatabasePath'."
    }
    [System.IO.File]::WriteAllLines(
        $OutputPath,
        [String[]] $csvLines,
        [System.Text.UTF8Encoding]::new($false)
    )
}

function AssertExternalVolumeMounted([String] $Path) {
    if ($IsMacOS -and $Path -match '^/Volumes/([^/]+)(?:/|$)') {
        $volumePath = "/Volumes/$($Matches[1])"
        if (-not (Test-Path -LiteralPath $volumePath -PathType Container)) {
            throw "External volume '$volumePath' is not mounted."
        }
    }
}

$sqlite = GetRequiredCommand 'sqlite3'

AssertExternalVolumeMounted $BackupDirectory
AssertExternalVolumeMounted $OutputDirectory

if (-not $DeviceUdid) {
    if ($SkipBackup) {
        $backupCandidates = @(
            Get-ChildItem -LiteralPath $BackupDirectory -Directory -ErrorAction SilentlyContinue |
                Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'Manifest.db') }
        )
        if ($backupCandidates.Count -ne 1) {
            throw "Specify -DeviceUdid; found $($backupCandidates.Count) device backups in '$BackupDirectory'."
        }
        $DeviceUdid = $backupCandidates[0].Name
    } else {
        $ideviceId = GetRequiredCommand 'idevice_id'
        $connectedDevices = @(& $ideviceId -l)
        if ($LASTEXITCODE -ne 0) {
            throw "'idevice_id' exited with code $LASTEXITCODE."
        }
        if ($connectedDevices.Count -ne 1) {
            throw "Specify -DeviceUdid; found $($connectedDevices.Count) connected iOS devices."
        }
        $DeviceUdid = $connectedDevices[0].Trim()
    }
}

if (-not $SkipBackup) {
    $idevicebackup2 = GetRequiredCommand 'idevicebackup2'
    [void] (New-Item -ItemType Directory -Path $BackupDirectory -Force)

    $backupArguments = @('-u', $DeviceUdid, 'backup')
    if ($FullBackup) {
        $backupArguments += '--full'
    }
    $backupArguments += $BackupDirectory

    Write-Host "Backing up device '$DeviceUdid' to '$BackupDirectory'..."
    InvokeNativeCommand $idevicebackup2 $backupArguments
}

$deviceBackupDirectory = Join-Path $BackupDirectory $DeviceUdid
$manifestPath = Join-Path $deviceBackupDirectory 'Manifest.db'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Backup manifest not found at '$manifestPath'."
}

$escapedBundleId = $BundleId.Replace("'", "''")
$coreLibraryRelativePath = 'Library/Application Support/CoreData/com.bn.CoreLibrary.sqlite'
$escapedRelativePath = $coreLibraryRelativePath.Replace("'", "''")
$manifestQuery = @"
SELECT fileID
FROM Files
WHERE domain = 'AppDomain-$escapedBundleId'
  AND relativePath = '$escapedRelativePath'
  AND flags = 1;
"@

$fileIds = @(& $sqlite $manifestPath $manifestQuery)
if ($LASTEXITCODE -ne 0) {
    throw "Could not query '$manifestPath'. Encrypted backups must be decrypted before extraction."
}
if ($fileIds.Count -ne 1 -or $fileIds[0] -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Nook Core Library database was not found in the backup manifest."
}

$fileId = $fileIds[0]
$sourceDatabasePath = Join-Path $deviceBackupDirectory (Join-Path $fileId.Substring(0, 2) $fileId)
if (-not (Test-Path -LiteralPath $sourceDatabasePath -PathType Leaf)) {
    throw "Manifest file '$fileId' is missing from the backup."
}

$outputDatabasePath = Join-Path $OutputDirectory 'nook-core-library.sqlite'
$lookupsCsvPath = Join-Path $OutputDirectory 'nook-dictionary-lookups.csv'
$publicationsCsvPath = Join-Path $OutputDirectory 'nook-publications.csv'
[void] (New-Item -ItemType Directory -Path $OutputDirectory -Force)
Copy-Item -LiteralPath $sourceDatabasePath -Destination $outputDatabasePath -Force

$lookupQuery = @"
SELECT
    Z_PK AS id,
    CASE
        WHEN ZCREATEDAT IS NULL THEN NULL
        ELSE datetime(ZCREATEDAT + 978307200, 'unixepoch', 'localtime')
    END AS created_at_local,
    ZEAN AS publication_id,
    ZLOOKUPTEXT AS lookup_text,
    ZNOTE AS note,
    ZPAGENUMBER AS page_number,
    ZCONTEXTSTARTLOCATION AS context_start_location,
    ZSTARTABSOFFSETSTRING AS start_offset,
    ZENDABSOFFSETSTRING AS end_offset
FROM ZBNLIBRARYLOOKUPS
ORDER BY ZCREATEDAT;
"@

$publicationQuery = @"
WITH lookup_counts AS (
    SELECT
        ZEAN AS publication_id,
        count(*) AS lookup_count
    FROM ZBNLIBRARYLOOKUPS
    GROUP BY ZEAN
)
SELECT
    lookup_counts.publication_id,
    max(coalesce(product.ZTITLESSEARCHSTRING, product.ZTITLESSORTSTRING)) AS title,
    max(product.ZAUTHORSSEARCHSTRING) AS authors,
    max(product.ZPUBLISHER) AS publisher,
    max(item.ZFILENAME) AS filename,
    lookup_counts.lookup_count
FROM lookup_counts
LEFT JOIN ZBNLIBRARYPRODUCT AS product
    ON product.ZEAN = lookup_counts.publication_id
LEFT JOIN ZBNLIBRARYITEM AS item
    ON item.ZPRODUCT = product.Z_PK
GROUP BY lookup_counts.publication_id, lookup_counts.lookup_count
ORDER BY title COLLATE NOCASE, lookup_counts.publication_id;
"@

ExportSqliteCsv $sqlite $sourceDatabasePath $lookupQuery $lookupsCsvPath 'Nook lookup records'
ExportSqliteCsv $sqlite $sourceDatabasePath $publicationQuery $publicationsCsvPath 'Nook publication metadata'

$recordCount = & $sqlite $sourceDatabasePath 'SELECT count(*) FROM ZBNLIBRARYLOOKUPS;'
if ($LASTEXITCODE -ne 0) {
    throw "Could not count Nook lookup records in '$sourceDatabasePath'."
}

[PSCustomObject] @{
    DeviceUdid = $DeviceUdid
    RecordCount = [Int64] $recordCount
    LookupsCsvPath = $lookupsCsvPath
    PublicationsCsvPath = $publicationsCsvPath
    DatabasePath = $outputDatabasePath
}
