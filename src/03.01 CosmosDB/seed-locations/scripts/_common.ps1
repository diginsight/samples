#requires -Version 5.1
<#
Shared helpers for the seed-locations scripts.

All source is ASCII-only to keep PowerShell 5.1 happy regardless of file
encoding (no em-dashes, no smart quotes, no non-breaking spaces).
#>

$ErrorActionPreference = 'Stop'

# Repo paths (resolved relative to this file)
$script:SeedRoot = Split-Path -Parent $PSScriptRoot
$script:RawDir   = Join-Path $SeedRoot 'raw'
$script:DataDir  = Join-Path $SeedRoot 'data'
$script:OutDir   = Join-Path $SeedRoot 'out'

foreach ($d in @($RawDir, $DataDir, $OutDir)) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d | Out-Null }
}

# -------------------------------------------------------------------
# Target Cosmos resources
#
# These values are NOT hard-coded in the public repo. They are loaded
# from an external configuration file in the *private* `samples.internal`
# repository (kept as a sibling clone of this repo), using exactly the
# same relative-path convention as the LocationAPI / IdentityAPI
# `launchSettings.json` files (see `ExternalConfigurationFolder`).
#
# Resolution order (first match wins for each variable):
#
#   1. External configuration file:
#        <ExternalConfigurationFolder>\src\03.01 CosmosDB\seed-locations\config\_common.local.ps1
#      where <ExternalConfigurationFolder> defaults to
#      `..\..\..\..\..\samples.internal` (relative to this script) and can
#      be overridden via the env var `ExternalConfigurationFolder`.
#
#   2. A local override file beside this script (also gitignored):
#        scripts/_common.local.ps1
#      An example is shipped as _common.local.ps1.example.
#
#   3. Environment variables (handy for CI):
#        $env:SEED_SUBSCRIPTION_ID
#        $env:SEED_TENANT_ID
#        $env:SEED_COSMOS_ACCOUNT
#        $env:SEED_COSMOS_RG
#        $env:SEED_COSMOS_DATABASE   (default 'location')
#        $env:SEED_COSMOS_CONTAINER  (default 'location')
# -------------------------------------------------------------------

# Defaults from env-vars (lowest precedence)
$script:SubscriptionId  = $env:SEED_SUBSCRIPTION_ID
$script:TenantId        = $env:SEED_TENANT_ID
$script:CosmosAccount   = $env:SEED_COSMOS_ACCOUNT
$script:CosmosRg        = $env:SEED_COSMOS_RG
$script:CosmosDatabase  = if ($env:SEED_COSMOS_DATABASE)  { $env:SEED_COSMOS_DATABASE }  else { 'location' }
$script:CosmosContainer = if ($env:SEED_COSMOS_CONTAINER) { $env:SEED_COSMOS_CONTAINER } else { 'location' }

# Layer 2: local override beside this script (gitignored)
$localOverride = Join-Path $PSScriptRoot '_common.local.ps1'
if (Test-Path $localOverride) {
    . $localOverride
}

# Layer 1 (highest precedence): external configuration file from the
# private `samples.internal` repo, resolved via a relative path that
# mirrors the `ExternalConfigurationFolder` convention used elsewhere.
$externalRoot = if ($env:ExternalConfigurationFolder) {
    $env:ExternalConfigurationFolder
} else {
    # scripts -> seed-locations -> 03.01 CosmosDB -> src -> samples -> ..
    Join-Path $PSScriptRoot '..\..\..\..\..\samples.internal'
}
$externalConfig = Join-Path $externalRoot 'src\03.01 CosmosDB\seed-locations\config\_common.local.ps1'
if (Test-Path $externalConfig) {
    Write-Verbose "Loading external config: $externalConfig"
    . $externalConfig
}

# Fail fast with a clear message if still missing
foreach ($name in @('SubscriptionId','TenantId','CosmosAccount','CosmosRg')) {
    if ([string]::IsNullOrWhiteSpace((Get-Variable -Scope Script -Name $name).Value)) {
        throw @"
Missing required config '$name'.
Either:
  - clone the private 'samples.internal' repo as a sibling of this one and
    create '<samples.internal>\src\03.01 CosmosDB\seed-locations\config\_common.local.ps1', or
  - set `$env:ExternalConfigurationFolder` to point at an alternative folder, or
  - create 'scripts/_common.local.ps1' (see _common.local.ps1.example), or
  - export `$env:SEED_$($name.ToUpper())`.
"@
    }
}

# ISO-2 codes considered "European" for filtering regions and cities.
$script:EuropeIso2 = @(
    'AD','AL','AT','AX','BA','BE','BG','BY','CH','CY','CZ','DE','DK','EE',
    'ES','FI','FO','FR','GB','GG','GI','GR','HR','HU','IE','IM','IS','IT',
    'JE','LI','LT','LU','LV','MC','MD','ME','MK','MT','NL','NO','PL','PT',
    'RO','RS','RU','SE','SI','SJ','SK','SM','TR','UA','VA','XK'
)

# ISO-2 codes for the Middle East. Cyprus (CY) and Turkey (TR) overlap with
# EuropeIso2 and are intentionally omitted here; the union below de-dupes
# via a HashSet so duplicates would be harmless anyway.
$script:MiddleEastIso2 = @(
    'AE','BH','EG','IL','IQ','IR','JO','KW','LB','OM','PS','QA','SA','SY','YE'
)

# ISO-2 codes for North America (GeoNames continent NA): the contiguous
# countries (Canada, United States, Mexico), Central America, the Caribbean,
# Greenland and the dependent territories.
$script:NorthAmericaIso2 = @(
    'AG','AI','AW','BB','BL','BM','BQ','BS','BZ','CA','CR','CU','CW','DM','DO',
    'GD','GL','GP','GT','HN','HT','JM','KN','KY','LC','MF','MQ','MS','MX','NI',
    'PA','PM','PR','SV','SX','TC','TT','US','VC','VG','VI'
)

# ISO-2 codes for the Asian countries currently in scope. Limited to India
# for now per the user request; extend this list to broaden coverage.
$script:AsiaIso2 = @(
    'IN'
)

# ISO-2 codes for South America (GeoNames continent SA). Central American
# countries (BZ, CR, GT, HN, NI, PA, SV) are already in NorthAmericaIso2 per
# GeoNames classification, so they are NOT duplicated here.
$script:SouthAmericaIso2 = @(
    'AR','BO','BR','CL','CO','EC','FK','GF','GY','PE','PY','SR','UY','VE'
)

# ISO-2 codes for Oceania (GeoNames continent OC): the Australian continent,
# New Zealand and the Pacific island nations / territories.
$script:OceaniaIso2 = @(
    'AS','AU','CK','FJ','FM','GU','KI','MH','MP','NC','NF','NR','NU','NZ',
    'PF','PG','PN','PW','SB','TK','TO','TV','UM','VU','WF','WS'
)

# Target ISO-2 set used by 05-build-regions.ps1 and 06-build-cities.ps1.
# = Europe + Middle East + North America + selected Asia + South America
# + Oceania (deduped).
$script:TargetIso2 = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]($script:EuropeIso2 + $script:MiddleEastIso2 + $script:NorthAmericaIso2 + $script:AsiaIso2 + $script:SouthAmericaIso2 + $script:OceaniaIso2),
    [System.StringComparer]::OrdinalIgnoreCase)

# Map mledoze "region" -> continent code
$script:RegionToContinent = @{
    'Africa'    = 'AF'
    'Antarctic' = 'AN'
    'Asia'      = 'AS'
    'Europe'    = 'EU'
    'Americas'  = 'NA'   # split further with subregion if needed
    'Oceania'   = 'OC'
}

# Mark this run with a fixed timestamp so _seed.loadedAt is stable per run
$script:SeedLoadedAt = ([datetime]::UtcNow).ToString('yyyy-MM-ddTHH:mm:ssZ')
$script:SeedVersion  = '1'

function New-DeterministicGuid {
    <#
    Produces a UUIDv5-like GUID from a string seed. Stable across runs ->
    upserts are idempotent.
    #>
    param([Parameter(Mandatory)][string]$Seed)

    $sha1  = [System.Security.Cryptography.SHA1]::Create()
    try {
        $bytes = $sha1.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Seed))
    } finally { $sha1.Dispose() }

    $guidBytes = New-Object byte[] 16
    [Array]::Copy($bytes, 0, $guidBytes, 0, 16)
    # version = 5 (name-based, SHA1)
    $guidBytes[6] = ([byte](($guidBytes[6] -band 0x0F) -bor 0x50))
    # variant = RFC 4122
    $guidBytes[8] = ([byte](($guidBytes[8] -band 0x3F) -bor 0x80))
    return ([Guid]::new($guidBytes)).ToString()
}

function New-GeoPoint {
    <#
    Build a GeoJSON Point. Coordinates are [longitude, latitude] per RFC 7946.
    Returns $null when either coordinate is missing.
    #>
    param(
        [Nullable[double]]$Latitude,
        [Nullable[double]]$Longitude
    )
    if ($null -eq $Latitude -or $null -eq $Longitude) { return $null }
    [ordered]@{
        type        = 'Point'
        coordinates = @([double]$Longitude, [double]$Latitude)
    }
}

function New-SeedBlock {
    <#
    Standard provenance block embedded in every seeded document.
    Named `seed` (no underscore) so the CosmosdbConsole NormalizeDocument
    routine, which strips all `_*` properties, leaves it alone.
    #>
    param(
        [Parameter(Mandatory)][string]$Source
    )
    [ordered]@{
        source   = $Source
        loadedAt = $script:SeedLoadedAt
        version  = $script:SeedVersion
    }
}

function Write-DocumentsJson {
    <#
    Wraps a sequence of hashtables/PSCustomObjects as { "Documents": [...] }
    and writes it to disk in the format expected by `CosmosdbConsole uploadjson`.
    #>
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][System.Collections.IEnumerable]$Documents
    )

    $envelope = [ordered]@{ Documents = @($Documents) }
    $json = $envelope | ConvertTo-Json -Depth 25
    # Always write UTF-8 with BOM so re-reads under PS 5.1 work.
    [System.IO.File]::WriteAllText(
        $Path, $json, (New-Object System.Text.UTF8Encoding($true)))
    Write-Host ("  wrote {0}  ({1} docs, {2:N0} bytes)" -f `
        $Path, $envelope.Documents.Count, (Get-Item $Path).Length)
}
