#requires -Version 5.1
<#
Step 0 -- Verify the container partition key path.
#>
. (Join-Path $PSScriptRoot '_common.ps1')

az account set --subscription $script:SubscriptionId | Out-Null

Write-Host "Container : $($script:CosmosAccount)/$($script:CosmosDatabase)/$($script:CosmosContainer)"
Write-Host "RG        : $($script:CosmosRg)"

$pk = az cosmosdb sql container show `
        --account-name  $script:CosmosAccount `
        --database-name $script:CosmosDatabase `
        --name          $script:CosmosContainer `
        --resource-group $script:CosmosRg `
        --query "resource.partitionKey" -o json | ConvertFrom-Json

Write-Host ""
Write-Host "Partition key: $($pk.paths -join ', ')  (kind=$($pk.kind), version=$($pk.version))"

if ($pk.paths[0] -ne '/partitionKey') {
    Write-Warning "Container PK is '$($pk.paths[0])'. The build scripts emit '/partitionKey'. Update _common.ps1 accordingly."
    exit 1
}
Write-Host "OK" -ForegroundColor Green
