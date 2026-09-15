<#
.SYNOPSIS
    Backs up the local `cems` PostgreSQL database to a timestamped file under Backend/backups/.

.DESCRIPTION
    Reads the connection string the same way the running app does -- straight out of the local
    `dotnet user-secrets` store (see README.md "Local setup") -- so there is nothing extra to
    configure. Writes a Postgres custom-format (-Fc) dump, the standard compressed format that
    `pg_restore` can restore selectively or in parallel, unlike a plain .sql file.

    Backups are gitignored (Backend/backups/) -- they are never meant to be committed.

    This only proves anything once a restore has actually been tested against it -- see
    restore-database.ps1 and the README's "Local backups" section.

.PARAMETER OutputDirectory
    Where to write the backup file. Defaults to Backend/backups/ (relative to the repo root).
#>

param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\Backend\backups")
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

$connectionString = Get-LocalConnectionString
$parts = Parse-ConnectionString $connectionString
$pgHost = $parts["Host"]
$pgPort = $parts["Port"]
$pgDatabase = $parts["Database"]
$pgUser = $parts["Username"]
$pgPassword = $parts["Password"]

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyy-MM-dd_HHmmss"
$outputFile = Join-Path $OutputDirectory "$($pgDatabase)_$timestamp.dump"

$pgDump = Find-PgTool "pg_dump"

$env:PGPASSWORD = $pgPassword
try {
    & $pgDump -h $pgHost -p $pgPort -U $pgUser -Fc -f $outputFile $pgDatabase
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump exited with code $LASTEXITCODE."
    }
} finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

$sizeKb = [math]::Round((Get-Item $outputFile).Length / 1KB, 1)
Write-Host "Backup written: $outputFile ($sizeKb KB)"
