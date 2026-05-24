#requires -Version 5.1
<#
Step 6 -- Build city documents from GeoNames cities15000.txt.
Filtered to the target ISO-2 set (Europe + Middle East, see _common.ps1).
Resolves regionName from the region docs produced by 05-build-regions.ps1
(if available).

cities15000 columns (TSV, 19 cols, see GeoNames readme):
   0 geonameid          11 admin2 code
   1 name               12 admin3 code
   2 asciiname          13 admin4 code
   3 alternatenames     14 population
   4 latitude           15 elevation
   5 longitude          16 dem
   6 feature class      17 timezone
   7 feature code       18 modification date
   8 country code
   9 cc2
  10 admin1 code
#>
. (Join-Path $PSScriptRoot '_common.ps1')

$src = Join-Path $script:DataDir 'cities15000.txt'
if (-not (Test-Path $src)) { throw "Missing $src. Run 02-download.ps1 first." }

# Load region names if available
$regionNames = @{}
$regionsOut = Join-Path $script:OutDir 'regions.json'
if (Test-Path $regionsOut) {
    Write-Host "Loading region names from regions.json ..."
    $regions = (Get-Content -Raw -LiteralPath $regionsOut -Encoding UTF8 | ConvertFrom-Json).Documents
    foreach ($r in $regions) {
        $regionNames["$($r.countryCode).$($r.code)"] = $r.name
    }
    Write-Host ("  {0} region keys loaded" -f $regionNames.Count)
} else {
    Write-Warning "regions.json not found. regionName will be empty. Run 05-build-regions.ps1 first."
}

$targetSet = $script:TargetIso2
$inv = [Globalization.CultureInfo]::InvariantCulture

Write-Host "Streaming $src ..."
$reader = [System.IO.StreamReader]::new(
    [System.IO.File]::Open($src, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read),
    [System.Text.Encoding]::UTF8)

$docs = [System.Collections.Generic.List[object]]::new()
try {
    $total = 0
    while ($null -ne ($line = $reader.ReadLine())) {
        $total++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $cols = $line.Split("`t")
        if ($cols.Length -lt 18) { continue }

        $cc = $cols[8]
        if (-not $targetSet.Contains($cc)) { continue }

        $geonameId = 0L; [void][long]::TryParse($cols[0], [ref]$geonameId)
        if ($geonameId -le 0) { continue }

        $altRaw = $cols[3]
        $alt    = if ([string]::IsNullOrWhiteSpace($altRaw)) { @() } else { @($altRaw.Split(',')) }

        $lat = 0.0; [void][double]::TryParse($cols[4], [Globalization.NumberStyles]::Float, $inv, [ref]$lat)
        $lon = 0.0; [void][double]::TryParse($cols[5], [Globalization.NumberStyles]::Float, $inv, [ref]$lon)

        $adm1 = $cols[10]
        $adm2 = $cols[11]
        $pop  = 0L; [void][long]::TryParse($cols[14], [ref]$pop)
        $elev = 0;  [void][int]::TryParse($cols[15], [ref]$elev)

        $regionName = $null
        $key = "$cc.$adm1"
        if ($regionNames.ContainsKey($key)) { $regionName = $regionNames[$key] }

        $doc = [ordered]@{
            id              = "city-$geonameId"
            type            = 'city'
            partitionKey    = $cc

            name            = $cols[1]
            asciiName       = $cols[2]
            alternateNames  = $alt

            countryCode     = $cc
            regionCode      = $adm1
            regionName      = $regionName
            adm2Code        = $adm2

            geoNameId       = $geonameId

            location        = (New-GeoPoint -Latitude $lat -Longitude $lon)
            latitude        = $lat
            longitude       = $lon

            elevation       = $elev
            population      = $pop
            timezone        = $cols[17]
            featureClass    = $cols[6]
            featureCode     = $cols[7]

            seed         = (New-SeedBlock -Source 'geonames:cities15000')
        }
        $docs.Add([pscustomobject]$doc)
    }
    Write-Host ("  scanned {0} rows, kept {1}" -f $total, $docs.Count)
}
finally { $reader.Dispose() }

$out = Join-Path $script:OutDir 'cities.json'
Write-DocumentsJson -Path $out -Documents $docs
