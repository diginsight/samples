#requires -Version 5.1
<#
Step 5 -- Build region (admin level 1) documents from GeoNames
admin1CodesASCII.txt. Filtered to the target ISO-2 set (Europe + Middle East,
see _common.ps1).

File format (TSV, 4 cols):
    1. concatenated codes: "<CC>.<ADM1>"
    2. name
    3. asciiName
    4. geonameId
#>
. (Join-Path $PSScriptRoot '_common.ps1')

$src = Join-Path $script:DataDir 'admin1CodesASCII.txt'
if (-not (Test-Path $src)) { throw "Missing $src. Run 02-download.ps1 first." }

Write-Host "Reading $src ..."
$lines = Get-Content -LiteralPath $src -Encoding UTF8
Write-Host ("  {0} input rows" -f $lines.Count)

$targetSet = $script:TargetIso2

$docs = foreach ($line in $lines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $cols = $line -split "`t"
    if ($cols.Length -lt 4) { continue }

    $parts = $cols[0].Split('.', 2)
    if ($parts.Length -ne 2) { continue }
    $cc   = $parts[0]
    $adm1 = $parts[1]

    if (-not $targetSet.Contains($cc)) { continue }

    $geonameId = 0L
    [void][long]::TryParse($cols[3], [ref]$geonameId)

    $doc = [ordered]@{
        id           = "region-$cc-$adm1"
        type         = 'region'
        partitionKey = $cc

        name         = $cols[1]
        asciiName    = $cols[2]

        countryCode  = $cc
        code         = $adm1
        iso31662     = "$cc-$adm1"
        level        = 1
        geoNameId    = $geonameId

        seed         = (New-SeedBlock -Source 'geonames:admin1CodesASCII')
    }
    [pscustomobject]$doc
}

$out = Join-Path $script:OutDir 'regions.json'
Write-DocumentsJson -Path $out -Documents $docs
