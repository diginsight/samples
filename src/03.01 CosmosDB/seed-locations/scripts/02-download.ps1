#requires -Version 5.1
<#
Step 2 -- Download the public datasets into ./raw and extract into ./data.
Skips files that already exist (delete them to force a refresh).
#>
. (Join-Path $PSScriptRoot '_common.ps1')

$sources = @(
    @{ Url='https://raw.githubusercontent.com/mledoze/countries/master/countries.json';
       File='countries.json'; Extract=$false },
    @{ Url='https://download.geonames.org/export/dump/countryInfo.txt';
       File='countryInfo.txt'; Extract=$false },
    @{ Url='https://download.geonames.org/export/dump/admin1CodesASCII.txt';
       File='admin1CodesASCII.txt'; Extract=$false },
    @{ Url='https://download.geonames.org/export/dump/cities15000.zip';
       File='cities15000.zip'; Extract=$true }
)

$ua = 'Mozilla/5.0 (compatible; diginsight-seed-locations/1.0)'

foreach ($s in $sources) {
    $rawPath = Join-Path $script:RawDir $s.File
    if (Test-Path $rawPath) {
        Write-Host "  skip (cached) : $($s.File)"
    } else {
        Write-Host "  download      : $($s.Url)"
        Invoke-WebRequest -Uri $s.Url -OutFile $rawPath -UseBasicParsing -UserAgent $ua
    }

    if ($s.Extract) {
        Write-Host "  extract       : $($s.File)  ->  data/"
        Expand-Archive -Path $rawPath -DestinationPath $script:DataDir -Force
    } else {
        Copy-Item -Path $rawPath -Destination (Join-Path $script:DataDir $s.File) -Force
    }
}

Write-Host ""
Write-Host "data/ contents:" -ForegroundColor Cyan
Get-ChildItem $script:DataDir | Select-Object Name, Length | Format-Table -AutoSize
