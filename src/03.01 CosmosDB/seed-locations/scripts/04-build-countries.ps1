#requires -Version 5.1
<#
Step 4 -- Build country documents.
Merges two sources:
  - mledoze/countries  (rich names, currencies, languages, flag, borders)
  - GeoNames countryInfo.txt (population, continent, postal codes, neighbours)
Output: ./out/countries.json
#>
. (Join-Path $PSScriptRoot '_common.ps1')

$srcMl = Join-Path $script:DataDir 'countries.json'
$srcGn = Join-Path $script:DataDir 'countryInfo.txt'
if (-not (Test-Path $srcMl)) { throw "Missing $srcMl. Run 02-download.ps1 first." }
if (-not (Test-Path $srcGn)) { throw "Missing $srcGn. Run 02-download.ps1 first." }

Write-Host "Reading $srcMl ..."
$countries = Get-Content -Raw -LiteralPath $srcMl -Encoding UTF8 | ConvertFrom-Json
Write-Host ("  {0} input countries (mledoze)" -f $countries.Count)

Write-Host "Reading $srcGn ..."
$gnByCc = @{}
foreach ($line in (Get-Content -LiteralPath $srcGn -Encoding UTF8)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line.StartsWith('#')) { continue }
    $cols = $line.Split("`t")
    if ($cols.Length -lt 19) { continue }
    $cc = $cols[0]
    if ([string]::IsNullOrWhiteSpace($cc)) { continue }

    $pop = 0L; [void][long]::TryParse($cols[7], [ref]$pop)
    $area = 0.0; [void][double]::TryParse($cols[6], [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$area)
    $gnId = 0L; [void][long]::TryParse($cols[16], [ref]$gnId)

    $gnByCc[$cc] = [pscustomobject]@{
        continent        = $cols[8]   # 2-letter code
        capitalName      = $cols[5]
        population       = $pop
        area             = $area
        phone            = $cols[12]
        postalCodeFormat = $cols[13]
        postalCodeRegex  = $cols[14]
        languageLocales  = if ([string]::IsNullOrWhiteSpace($cols[15])) { @() } else { @($cols[15].Split(',')) }
        geoNameId        = $gnId
        neighbours       = if ([string]::IsNullOrWhiteSpace($cols[17])) { @() } else { @($cols[17].Split(',')) }
    }
}
Write-Host ("  {0} input countries (GeoNames)" -f $gnByCc.Count)

$docs = foreach ($c in $countries) {
    $cca2 = [string]$c.cca2
    if ([string]::IsNullOrWhiteSpace($cca2)) { continue }

    $commonName   = [string]$c.name.common
    $officialName = [string]$c.name.official

    # Native names: project the dictionary to an array of {lang, common, official}
    $nativeNames = @()
    if ($c.name.native) {
        foreach ($p in $c.name.native.PSObject.Properties) {
            $nativeNames += [ordered]@{
                lang     = $p.Name
                common   = [string]$p.Value.common
                official = [string]$p.Value.official
            }
        }
    }

    # Currencies -> array of {code, name, symbol}
    $currencies = @()
    if ($c.currencies) {
        foreach ($p in $c.currencies.PSObject.Properties) {
            $currencies += [ordered]@{
                code   = $p.Name
                name   = [string]$p.Value.name
                symbol = [string]$p.Value.symbol
            }
        }
    }

    # Languages -> array of {iso639_3, name}
    $languages = @()
    if ($c.languages) {
        foreach ($p in $c.languages.PSObject.Properties) {
            $languages += [ordered]@{
                iso639_3 = $p.Name
                name     = [string]$p.Value
            }
        }
    }

    # Calling codes "+39" form
    $callingCodes = @()
    if ($c.callingCodes) { $callingCodes = @($c.callingCodes | ForEach-Object { "+$_" }) }
    elseif ($c.idd -and $c.idd.root) {
        $root = [string]$c.idd.root
        if ($c.idd.suffixes -and $c.idd.suffixes.Count -gt 0) {
            $callingCodes = @($c.idd.suffixes | ForEach-Object { "$root$_" })
        } else {
            $callingCodes = @($root)
        }
    }

    # Lat/Lng
    $lat = $null; $lon = $null
    if ($c.latlng -and $c.latlng.Count -ge 2) {
        $lat = [double]$c.latlng[0]; $lon = [double]$c.latlng[1]
    }

    # GeoNames enrichment
    $gn = $null
    if ($gnByCc.ContainsKey($cca2)) { $gn = $gnByCc[$cca2] }

    # Continent code: prefer GeoNames, fall back to mledoze region mapping
    $continentCode = $null
    if ($gn -and $gn.continent) { $continentCode = $gn.continent }
    elseif ($c.region) {
        $r = [string]$c.region
        if ($script:RegionToContinent.ContainsKey($r)) {
            $continentCode = $script:RegionToContinent[$r]
        }
        if ($r -eq 'Americas') {
            $sub = [string]$c.subregion
            if ($sub -eq 'South America') { $continentCode = 'SA' } else { $continentCode = 'NA' }
        }
    }

    # Population, area, phone, postal: prefer GeoNames
    $population = if ($gn) { $gn.population } else { $null }
    $area       = if ($null -ne $c.area) { [double]$c.area } elseif ($gn) { $gn.area } else { $null }
    $phone      = if ($gn) { $gn.phone } else { $null }
    if (-not $callingCodes -or $callingCodes.Count -eq 0) {
        if ($phone) { $callingCodes = @($phone.Split(',') | ForEach-Object { "+" + $_.Trim() }) }
    }

    $flagSvg = if ([string]::IsNullOrEmpty($cca2)) { $null } else { "https://flagcdn.com/$($cca2.ToLowerInvariant()).svg" }

    $doc = [ordered]@{
        id              = "country-$cca2"
        type            = 'country'
        partitionKey    = $cca2

        name            = $commonName
        officialName    = $officialName
        nativeNames     = $nativeNames
        altSpellings    = @($c.altSpellings)

        code            = $cca2
        code3           = [string]$c.cca3
        numericCode     = [string]$c.ccn3

        continentCode   = $continentCode
        region          = [string]$c.region
        subregion       = [string]$c.subregion

        capital         = if ($c.capital -and $c.capital.Count -gt 0) { [string]$c.capital[0] } elseif ($gn) { $gn.capitalName } else { $null }

        location        = (New-GeoPoint -Latitude $lat -Longitude $lon)
        latitude        = $lat
        longitude       = $lon

        area            = $area
        population      = $population

        languages       = $languages
        languageLocales = if ($gn) { $gn.languageLocales } else { @() }
        currencies      = $currencies
        callingCodes    = $callingCodes
        tlds            = @($c.tld)
        borders         = @($c.borders)

        postalCode      = [ordered]@{
            format = if ($gn) { $gn.postalCodeFormat } else { $null }
            regex  = if ($gn) { $gn.postalCodeRegex }  else { $null }
        }

        neighbours      = if ($gn) { $gn.neighbours } else { @() }
        geoNameId       = if ($gn) { $gn.geoNameId } else { $null }

        flag            = [ordered]@{
            emoji = [string]$c.flag
            svg   = $flagSvg
        }

        landlocked      = [bool]$c.landlocked
        independent     = [bool]$c.independent

        seed         = (New-SeedBlock -Source 'mledoze/countries+geonames/countryInfo')
    }
    [pscustomobject]$doc
}

$out = Join-Path $script:OutDir 'countries.json'
Write-DocumentsJson -Path $out -Documents $docs
