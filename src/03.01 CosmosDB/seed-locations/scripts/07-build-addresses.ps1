#requires -Version 5.1
<#
Step 8 (optional) -- Build synthetic address documents jittered around real
cities loaded in out/cities.json.
#>
param(
    [int]$Count = 2000,
    [int]$Seed  = 4242
)
. (Join-Path $PSScriptRoot '_common.ps1')

$citiesOut = Join-Path $script:OutDir 'cities.json'
if (-not (Test-Path $citiesOut)) { throw "Missing $citiesOut. Run 06-build-cities.ps1 first." }

$cities = (Get-Content -Raw -LiteralPath $citiesOut -Encoding UTF8 | ConvertFrom-Json).Documents
Write-Host ("  source cities: {0}" -f $cities.Count)

# Deterministic random
$rng = [System.Random]::new($Seed)

$streetStems = @('Main','Park','High','Station','Mill','Church','Market','Old','New','Garden','School','Hill','River','King','Queen')
$streetTypes = @('Street','Road','Avenue','Lane','Boulevard','Place','Square','Way','Drive')

$docs = for ($i = 0; $i -lt $Count; $i++) {
    $city = $cities[$rng.Next(0, $cities.Count)]
    $lat0 = [double]$city.latitude
    $lon0 = [double]$city.longitude
    $lat  = $lat0 + ($rng.NextDouble() - 0.5) * 0.02
    $lon  = $lon0 + ($rng.NextDouble() - 0.5) * 0.02

    $street     = "$($streetStems[$rng.Next(0,$streetStems.Length)]) $($streetTypes[$rng.Next(0,$streetTypes.Length)])"
    $house      = "$($rng.Next(1,300))"
    $postal     = "{0:D5}" -f $rng.Next(1000, 99999)

    $seedKey    = "address:$($city.id):$i"
    $uuid       = New-DeterministicGuid -Seed $seedKey

    $doc = [ordered]@{
        id           = "address-$uuid"
        type         = 'address'
        partitionKey = [string]$city.countryCode

        name         = "$street $house, $($city.name)"

        countryCode  = [string]$city.countryCode
        cityId       = [string]$city.id
        cityName     = [string]$city.name
        regionCode   = [string]$city.regionCode

        street       = $street
        houseNumber  = $house
        postalCode   = $postal

        location     = (New-GeoPoint -Latitude $lat -Longitude $lon)
        latitude     = $lat
        longitude    = $lon

        seed         = (New-SeedBlock -Source 'generated:addresses')
    }
    [pscustomobject]$doc
}

$out = Join-Path $script:OutDir 'addresses.json'
Write-DocumentsJson -Path $out -Documents $docs
