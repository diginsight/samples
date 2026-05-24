#requires -Version 5.1
<#
Step 1 -- Determine the Cosmos endpoint and load it into the current shell.
If the account has local auth disabled (now the case), we just export the
endpoint URL into $env:COSMOS_CONN and CosmosdbConsole will use
DefaultAzureCredential. Otherwise we pull the key connection string.
#>
. (Join-Path $PSScriptRoot '_common.ps1')

az account set --subscription $script:SubscriptionId | Out-Null

$acc = az cosmosdb show `
        --name $script:CosmosAccount `
        --resource-group $script:CosmosRg `
        --query "{endpoint:documentEndpoint, disableLocalAuth:disableLocalAuth}" -o json | ConvertFrom-Json

if ($acc.disableLocalAuth) {
    Write-Host "Local auth disabled. Using AAD via DefaultAzureCredential." -ForegroundColor Yellow
    $env:COSMOS_CONN = $acc.endpoint
    [Environment]::SetEnvironmentVariable('COSMOS_CONN', $acc.endpoint, 'Process')
    Write-Host "COSMOS_CONN set to endpoint: $($acc.endpoint)" -ForegroundColor Green
} else {
    $conn = az cosmosdb keys list `
                --type connection-strings `
                --name $script:CosmosAccount `
                --resource-group $script:CosmosRg `
                --query "connectionStrings[0].connectionString" -o tsv
    if ([string]::IsNullOrWhiteSpace($conn)) {
        throw "Failed to retrieve a connection string for $($script:CosmosAccount)."
    }
    $env:COSMOS_CONN = $conn
    [Environment]::SetEnvironmentVariable('COSMOS_CONN', $conn, 'Process')
    Write-Host "COSMOS_CONN set in this shell session (length=$($conn.Length))." -ForegroundColor Green
}
