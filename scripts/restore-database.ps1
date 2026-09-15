<#
.SYNOPSIS
    Restores a backup created by backup-database.ps1.

.DESCRIPTION
    Restores into a NEW, separate database by default (named "<database>_restore_test") --
    never the real database you're actively using -- so running this can't accidentally clobber
    real data. Pass -Overwrite to instead restore directly onto the real database name from your
    connection string, which is destructive and replaces everything currently in it.

    Uses `pg_restore --clean --if-exists`, so restoring into an existing target (e.g. running this
    script twice against the default scratch database) drops conflicting objects first rather
    than failing.

.PARAMETER BackupFile
    Path to a .dump file created by backup-database.ps1.

.PARAMETER TargetDatabase
    Database to restore into. Defaults to "<database>_restore_test" (created if it doesn't exist).

.PARAMETER Overwrite
    Restore directly onto the real database name from your connection string instead of the safe
    scratch database. Destructive -- replaces everything currently in that database.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,

    [string]$TargetDatabase,

    [switch]$Overwrite
)

$ErrorActionPreference = "Stop"

function Find-PgTool {
    param([string]$Name)
    $onPath = Get-Command $Name -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $candidates = Get-ChildItem "C:\Program Files\PostgreSQL\*\bin\$Name.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending
    if ($candidates) { return $candidates[0].FullName }
    throw "$Name.exe not found on PATH or under C:\Program Files\PostgreSQL\*\bin. Install the PostgreSQL client tools or add them to PATH."
}

function Get-LocalConnectionString {
    $csprojPath = Join-Path $PSScriptRoot "..\Backend\src\CEMS.Api\CEMS.Api.csproj"
    $csprojContent = Get-Content $csprojPath -Raw
    if ($csprojContent -notmatch '<UserSecretsId>([^<]+)</UserSecretsId>') {
        throw "Could not find <UserSecretsId> in CEMS.Api.csproj. Run 'dotnet user-secrets init' from Backend/src/CEMS.Api first (see README.md 'Local setup')."
    }
    $userSecretsId = $Matches[1]
    $secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$userSecretsId\secrets.json"
    if (-not (Test-Path $secretsPath)) {
        throw "No secrets.json found at $secretsPath. Set ConnectionStrings:Default via 'dotnet user-secrets set' first (see README.md 'Local setup')."
    }
    $secrets = Get-Content $secretsPath -Raw | ConvertFrom-Json
    $connectionString = $secrets."ConnectionStrings:Default"
    if (-not $connectionString) {
        throw "ConnectionStrings:Default is not set in $secretsPath."
    }
    return $connectionString
}

function Parse-ConnectionString {
    param([string]$ConnectionString)
    $parts = @{}
    foreach ($pair in $ConnectionString -split ';') {
        if ($pair -match '^\s*([^=]+)=(.*)$') {
            $parts[$Matches[1].Trim()] = $Matches[2].Trim()
        }
    }
    return $parts
}

if (-not (Test-Path $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

$connectionString = Get-LocalConnectionString
$parts = Parse-ConnectionString $connectionString
$pgHost = $parts["Host"]
$pgPort = $parts["Port"]
$realDatabase = $parts["Database"]
$pgUser = $parts["Username"]
$pgPassword = $parts["Password"]

if (-not $TargetDatabase) {
    $TargetDatabase = if ($Overwrite) { $realDatabase } else { "$($realDatabase)_restore_test" }
}

if ($TargetDatabase -eq $realDatabase -and -not $Overwrite) {
    throw "Refusing to restore onto '$realDatabase' without -Overwrite. Pass -Overwrite explicitly if that's really what you want."
}

$psql = Find-PgTool "psql"
$createdb = Find-PgTool "createdb"
$pgRestore = Find-PgTool "pg_restore"

$env:PGPASSWORD = $pgPassword
try {
    $existsRaw = & $psql -h $pgHost -p $pgPort -U $pgUser -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$TargetDatabase'"
    $exists = if ($null -ne $existsRaw) { ($existsRaw | Out-String).Trim() } else { "" }
    if ($exists -ne "1") {
        Write-Host "Creating database '$TargetDatabase'..."
        & $createdb -h $pgHost -p $pgPort -U $pgUser $TargetDatabase
        if ($LASTEXITCODE -ne 0) { throw "createdb exited with code $LASTEXITCODE." }
    } elseif ($TargetDatabase -eq $realDatabase) {
        Write-Warning "Restoring onto '$realDatabase' -- this replaces everything currently in it."
    }

    Write-Host "Restoring $BackupFile into '$TargetDatabase'..."
    & $pgRestore -h $pgHost -p $pgPort -U $pgUser -d $TargetDatabase --clean --if-exists $BackupFile
    if ($LASTEXITCODE -ne 0) {
        throw "pg_restore exited with code $LASTEXITCODE."
    }
} finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Write-Host "Restored into '$TargetDatabase'."
