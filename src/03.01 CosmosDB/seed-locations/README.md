# seed-locations

Local working folder that seeds the `location` container of a Cosmos DB
account with public geo data (world countries + regions + cities for
Europe, the Middle East, North America, India, South America and Oceania
+ sample addresses).

The target subscription / tenant / Cosmos account / RG are **not**
hard-coded in this repo. They are loaded from the private `samples.internal`
repo at
`<samples.internal>\src\03.01 CosmosDB\seed-locations\config\_common.local.ps1`
via the same `ExternalConfigurationFolder` relative-path convention used
by the LocationAPI / IdentityAPI `launchSettings.json` (defaults to
`..\..\..\..\..\samples.internal` next to this repo). See
[`scripts/_common.ps1`](scripts/_common.ps1) for the resolution order
(external file -> in-repo `_common.local.ps1` -> env vars).

See the full plan in
[`docs/80. Userstories/202605/20260524.02-pupulate-locations/01.location-download-plan.md`](../../docs/80.%20Userstories/202605/20260524.02-pupulate-locations/01.location-download-plan.md).

## Quick run

```powershell
cd "src/03.01 CosmosDB/seed-locations"

# 0) verify container partition key (idempotent)
./scripts/00-verify-pk.ps1

# 1) load the connection string into the shell session
./scripts/01-connect.ps1     # sets $env:COSMOS_CONN

# 2) download raw public data into ./raw and ./data
./scripts/02-download.ps1

# 3) build Documents JSON files under ./out
./scripts/03-build-continents.ps1
./scripts/04-build-countries.ps1
./scripts/05-build-regions.ps1
./scripts/06-build-cities.ps1
./scripts/07-build-addresses.ps1   # optional

# 4) upload via the existing CosmosdbConsole
./scripts/99-upload.ps1
```

All scripts are **idempotent** -- `id` values are deterministic
(type-prefixed natural keys), so re-runs upsert in place.
