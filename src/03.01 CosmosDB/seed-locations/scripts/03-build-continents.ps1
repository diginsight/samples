#requires -Version 5.1
<#
Step 3 -- Build continent documents (small, hardcoded reference set).
Output: ./out/continents.json
#>
. (Join-Path $PSScriptRoot '_common.ps1')

$continents = @(
    @{ code='AF'; name='Africa' },
    @{ code='AN'; name='Antarctica' },
    @{ code='AS'; name='Asia' },
    @{ code='EU'; name='Europe' },
    @{ code='NA'; name='North America' },
    @{ code='OC'; name='Oceania' },
    @{ code='SA'; name='South America' }
)

$docs = foreach ($c in $continents) {
    $doc = [ordered]@{
        id           = "continent-$($c.code)"
        type         = 'continent'
        partitionKey = 'world'
        name         = $c.name
        code         = $c.code
        seed         = (New-SeedBlock -Source 'hardcoded:continents')
    }
    [pscustomobject]$doc
}

$out = Join-Path $script:OutDir 'continents.json'
Write-DocumentsJson -Path $out -Documents $docs
