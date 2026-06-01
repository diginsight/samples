#requires -Version 5.1
<#
Step 7 -- Upload all built JSON files to Cosmos via the existing
CosmosdbConsole `uploadjson` command. Idempotent.

Requires $env:COSMOS_CONN -- run 01-connect.ps1 first.
#>
. (Join-Path $PSScriptRoot '_common.ps1')

if ([string]::IsNullOrWhiteSpace($env:COSMOS_CONN)) {
    throw "COSMOS_CONN is not set. Run 01-connect.ps1 first."
}

$consoleProj = Resolve-Path (Join-Path $script:SeedRoot '..\CosmosdbConsole\CosmosdbConsole.csproj')

$files = @(
    'continents.json',
    'countries.json',
    'regions.json',
    'cities.json',
    'addresses.json'
)

foreach ($f in $files) {
    $path = Join-Path $script:OutDir $f
    if (-not (Test-Path $path)) {
        Write-Warning "Skipping $f (not built yet)"
        continue
    }
    Write-Host ""
    Write-Host "==> Uploading $f ..." -ForegroundColor Cyan
    & dotnet run --project $consoleProj --no-launch-profile -- uploadjson `
        -f $path `
        -c $env:COSMOS_CONN `
        -d $script:CosmosDatabase `
        -t $script:CosmosContainer `
        -s "_etag,_rid,_self,_ts,_attachments"
    # Cocona returns the int document count as the process exit code,
    # so a positive value means success.
    if ($LASTEXITCODE -lt 0) {
        throw "uploadjson failed for $f (exit $LASTEXITCODE)"
    }
    Write-Host ("    {0}: {1} documents upserted" -f $f, $LASTEXITCODE) -ForegroundColor Green
}

Write-Host ""
Write-Host "Upload complete." -ForegroundColor Green
