# OVERVIEW: Populate Cosmos DB `location.location` with worldwide & European geo seed data

**Date:** 2026-05-24
**Author:** Dario Airoldi
**Status:** Resolved
**Severity:** N/A (feature work / data seeding)
**Component:** `<cosmos-account>` / `location` / `location` &nbsp;·&nbsp; `CosmosdbConsole` &nbsp;·&nbsp; `seed-locations` pipeline
**Target Framework:** .NET 10 (`CosmosdbConsole`) · PowerShell 5.1 (build scripts)

---

## 🧭 Original User Request

> *"I wish to fill my cosmosdb with sample data about locations and Countries.
> Can you help me find some public dataset? E.g. with worldwide countries
> and (at least) European locations?"*

---

## 📋 Table of Contents

1. [📝 Description](#-description)
2. [🔍 Context Information](#-context-information)
3. [🔬 Analysis & Design Choices](#-analysis--design-choices)
4. [🔄 How Data Was Obtained](#-how-data-was-obtained)
5. [✅ Solution Implemented](#-solution-implemented)
6. [⬆️ How Data Was Uploaded](#️-how-data-was-uploaded)
7. [📚 Additional Information](#-additional-information)
8. [✔️ Resolution Status](#️-resolution-status)
9. [🎓 Lessons Learned](#-lessons-learned)
10. [📎 Appendix](#-appendix)

---

## 📝 DESCRIPTION

The user requested to populate the existing Cosmos DB container
`<cosmos-account> / location / location` with realistic public sample
data covering **worldwide countries** and **(at least) European locations**, to
exercise the `LocationAPI` and any downstream consumer.

The work delivered an idempotent, end‑to‑end seeding pipeline that:

1. Downloads open public datasets (REST‑Countries / mledoze + GeoNames).
2. Re‑shapes them into a clean, modern JSON schema tailored to Cosmos DB
   (single mixed‑type container, partitioned by country code, GeoJSON
   geometry, type‑prefixed natural IDs).
3. Uploads them via the existing [`CosmosdbConsole`](../../../03.01%20CosmosDB/CosmosdbConsole) CLI, which was extended with
   **AAD (`DefaultAzureCredential`)** support because the target account has
   `disableLocalAuth=true`.

### Final Result

| Document type | Count | Partition keys | Notes |
|---|---:|---|---|
| `continent` | 7 | `world` | Reference data |
| `country` | 250 | ISO‑2 (e.g. `IT`, `FR`) | World‑wide |
| `region` | 1,183 | ISO‑2 | Europe only (admin level 1) |
| `city` | 8,527 | ISO‑2 | Europe only, population ≥ 15 000 |
| `address` (optional) | — | ISO‑2 | Generator ready, not executed |
| **Total in container** | **9,967** | | Verified via `az cosmosdb sql container show` |

### Impact

- `LocationAPI` and `CosmosdbConsole` now have a non‑empty, realistic dataset
  to query against.
- A repeatable, documented pipeline exists under
  [`src/03.01 CosmosDB/seed-locations/`](../../../03.01%20CosmosDB/seed-locations/) so the data can be regenerated or
  extended (e.g. non‑European regions/cities) without manual work.
- `CosmosdbConsole` is now compatible with key‑less Cosmos DB accounts —
  a prerequisite for any environment where local auth is disabled.

---

## 🔍 CONTEXT INFORMATION

### Environment

> Real subscription / tenant / account / RG values are kept in the private
> `samples.internal` repo at
> [`src/03.01 CosmosDB/seed-locations/config/_common.local.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/_common.ps1)
> and loaded by the seed scripts via the same `ExternalConfigurationFolder`
> relative-path convention used by the LocationAPI / IdentityAPI
> `launchSettings.json`.

- **Subscription:** `<subscription-id>`
- **Tenant:** `<tenant-id>`
- **Resource Group:** `<resource-group>`
- **Cosmos Account:** `<cosmos-account>`
- **Endpoint:** `https://<cosmos-account>.documents.azure.com:443/`
- **Database / Container:** `location` / `location`
- **Partition key path:** `/partitionKey` (Hash, version 2) — verified
- **Local auth:** `disableLocalAuth = true` (AAD only)

### Pre-existing Code (relevant to the design)

| Element | Path | Role |
|---|---|---|
| `LocationBase` model | [Models/LocationBase.cs](../../../03.01%20CosmosDB/LocationAPI/Models/LocationBase.cs) | Existing PascalCase audit‑oriented model |
| `Country : LocationBase` | [Models/Country.cs](../../../03.01%20CosmosDB/LocationAPI/Models/Country.cs) | Legacy `Code`/`Name` shape |
| `LocationController` | [Controllers/LocationController.cs](../../../03.01%20CosmosDB/LocationAPI/Controllers/LocationController.cs) | Queries with `WHERE c.Type = @type` (**PascalCase**) |
| `CosmosdbConsole` | [src/03.01 CosmosDB/CosmosdbConsole](../../../03.01%20CosmosDB/CosmosdbConsole) | Cocona CLI with `uploadjson` / `query` / `delete` commands |
| `Executor.transform()` | [Executor.cs](../../../03.01%20CosmosDB/CosmosdbConsole/Executor.cs) | Stamps `partitionKey` and **strips any `_*` metadata properties** on upload |

> The user explicitly confirmed *"schema for location items should not necessarily
> reflect schema for code into the current repository — please ensure best data
> schema is chosen for location items"*, which freed the design from the
> existing PascalCase / audit‑heavy shape.

---

## 🔬 ANALYSIS & DESIGN CHOICES

A full option matrix is recorded in [01.location-download-plan.md](./01.location-download-plan.md). The key
decisions and their rationale are summarised here.

### 1. Container shape — *single mixed‑type container, country‑sharded*

| Option | Shape | Outcome |
|---|---|---|
| A. Fully embedded | One mega‑doc per country with `regions[]` + `cities[]` | ❌ Hits doc‑size limit, single‑city update rewrites country |
| **B. Mixed types, country‑sharded** | Every entity is its own doc; `partitionKey = ISO‑2` for country/region/city/address; `partitionKey = "world"` for continents | ✅ **Chosen** — small docs, even distribution (~250 LPs), "everything about Italy" is single‑partition |
| C. Type as partition key | `partitionKey = "country" / "region" / "city"` | ❌ Hot LP for cities (~8 500 in one LP), poor scalability |
| D. Synthetic composite PK | e.g. `"city:IT"` | ❌ No benefit over B, breaks single‑partition reads |

### 2. Property casing — *camelCase*

Picked **camelCase** over PascalCase even though the existing C# models use
PascalCase. Rationale: matches every upstream source (REST‑Countries,
GeoNames, Cosmos SDK defaults), is the JSON industry standard, and C# can
opt in with `JsonNamingPolicy.CamelCase`. The user explicitly authorised
diverging from the legacy schema.

### 3. `id` strategy — *type‑prefixed natural key*

| Type | `id` format | Example |
|---|---|---|
| continent | `continent-{code}` | `continent-EU` |
| country | `country-{iso2}` | `country-IT` |
| region | `region-{iso2}-{adm1}` | `region-IT-25` |
| city | `city-{geoNameId}` | `city-3173435` |
| address | `address-{uuid v5}` | `address-1cb1…` |

Deterministic ⇒ idempotent upserts. Human‑readable ⇒ trivial
point‑reads with `(id, partitionKey)`. Type‑prefixed ⇒ no collisions even
within the same logical partition.

### 4. Geometry — *GeoJSON Point + convenience scalars*

Every spatial doc carries:

```json
"location":  { "type": "Point", "coordinates": [12.4964, 41.9028] },
"latitude":  41.9028,
"longitude": 12.4964
```

GeoJSON enables `ST_DISTANCE` / `ST_WITHIN` queries with a spatial index;
the scalar pair keeps simple consumers (UI / charts) trivial.

### 5. Discriminator & provenance

- `type` (camelCase, lowercase value: `continent` / `country` / `region` / `city` / `address`).
- A small `seed` block (NOT `_seed`) is embedded on every document:

   ```json
   "seed": { "source": "geonames+mledoze", "loadedAt": "2026-05-24T…", "version": "1" }
   ```

   `_seed` was the first attempt but `CosmosdbConsole.NormalizeDocument()`
   **unconditionally strips any `_*` property** before upload — that
   metadata would have been silently lost. Renamed to `seed`.

### 6. Final partition‑key layout

| Document type | `partitionKey` | # logical partitions | Typical docs/LP |
|---|---|---:|---:|
| `continent` | `"world"` | 1 | 7 |
| `country` | ISO‑2 | ~250 | 1 |
| `region` | ISO‑2 | ~54 (Europe) | 1–60 |
| `city` | ISO‑2 | ~54 (Europe) | 10–800 |
| `address` | ISO‑2 | ~54 | 10–100 |

---

## 🔄 HOW DATA WAS OBTAINED

Four open public datasets are downloaded once and cached into `raw/`:

| Dataset | URL | Used for |
|---|---|---|
| **mledoze/countries** (raw JSON) | `https://raw.githubusercontent.com/mledoze/countries/master/dist/countries.json` | Country names (native, official, alt spellings), ISO codes, capitals, area, languages, currencies, calling codes, TLDs, borders, flag (emoji + SVG), landlocked/independent |
| **GeoNames countryInfo.txt** | `https://download.geonames.org/export/dump/countryInfo.txt` | Population, postal‑code format/regex, continent, phone, neighbours, language locales (enrichment over mledoze) |
| **GeoNames admin1CodesASCII.txt** | `https://download.geonames.org/export/dump/admin1CodesASCII.txt` | First‑level administrative divisions (regions/states) — Europe filtered |
| **GeoNames cities15000.zip** | `https://download.geonames.org/export/dump/cities15000.zip` | Cities with population ≥ 15 000 incl. lat/long, timezone, elevation, feature class — Europe filtered |

All datasets are released under permissive licences (mledoze: ODbL/CC‑BY;
GeoNames: CC‑BY 4.0).

### Build pipeline ([src/03.01 CosmosDB/seed-locations/scripts/](../../../03.01%20CosmosDB/seed-locations/scripts/))

Eight PowerShell 5.1 scripts (ASCII source — no Unicode, to remain
compatible with stock Windows PowerShell), all idempotent:

| Script | Purpose | Output |
|---|---|---|
| `_common.ps1` | Shared helpers (`New-DeterministicGuid`, `New-GeoPoint`, `New-SeedBlock`, `Write-DocumentsJson`), Europe ISO list, region→continent map | — |
| `00-verify-pk.ps1` | Asserts container PK is `/partitionKey` | console |
| `01-connect.ps1` | Detects `disableLocalAuth`; exports either the **endpoint URL** (AAD) or a key‑based connection string into `$env:COSMOS_CONN` | env var |
| `02-download.ps1` | Downloads & caches the 4 datasets into `raw/`; unzips into `data/` | `data/*.json|*.txt` |
| `03-build-continents.ps1` | 7 hard‑coded continent docs (`partitionKey="world"`) | `out/continents.json` |
| `04-build-countries.ps1` | Merges mledoze + GeoNames countryInfo.txt | `out/countries.json` (**250 docs**) |
| `05-build-regions.ps1` | Streams `admin1CodesASCII.txt`; filters Europe; type `region` | `out/regions.json` (**1 183 docs**) |
| `06-build-cities.ps1` | Streams `cities15000.txt`; filters Europe; resolves `regionName` from `regions.json` | `out/cities.json` (**8 527 docs**) |
| `07-build-addresses.ps1` | *(Optional — not executed)* Picks random cities, jitters ±0.01°, generates synthetic Street/HouseNumber/PostalCode | `out/addresses.json` |
| `99-upload.ps1` | Iterates `out/*.json` and invokes `CosmosdbConsole uploadjson` | upserts to Cosmos |

Each output JSON file follows the shape expected by `CosmosdbConsole`:

```json
{ "Documents": [ { "id": "...", "partitionKey": "...", "type": "...", ... } ] }
```

---

## ✅ SOLUTION IMPLEMENTED

### A. Seed‑data schema (per type)

Excerpt — full plan in [01.location-download-plan.md](./01.location-download-plan.md):

```jsonc
// country
{
  "id": "country-IT",
  "partitionKey": "IT",
  "type": "country",
  "name": "Italy",
  "officialName": "Italian Republic",
  "nativeNames": [{ "lang": "ita", "common": "Italia", "official": "Repubblica Italiana" }],
  "code": "IT", "code3": "ITA", "numericCode": "380",
  "continentCode": "EU", "region": "Europe", "subregion": "Southern Europe",
  "capital": ["Rome"],
  "location":  { "type": "Point", "coordinates": [12.4964, 41.9028] },
  "latitude": 41.9028, "longitude": 12.4964,
  "area": 301336, "population": 60317116,
  "languages": ["ita"], "languageLocales": ["it-IT"],
  "currencies": ["EUR"], "callingCodes": ["+39"], "tlds": [".it"],
  "borders": ["AUT","FRA","SMR","SVN","CHE","VAT"],
  "postalCode": { "format": "#####", "regex": "^(\\d{5})$" },
  "neighbours": ["AT","FR","SM","SI","CH","VA"],
  "geoNameId": 3175395,
  "flag": { "emoji": "🇮🇹", "svg": "https://flagcdn.com/it.svg" },
  "landlocked": false, "independent": true,
  "seed": { "source": "geonames+mledoze", "loadedAt": "2026-05-24T...", "version": "1" }
}
```

```jsonc
// region (admin level 1)
{
  "id": "region-IT-25", "partitionKey": "IT", "type": "region",
  "name": "Lombardy", "asciiName": "Lombardy",
  "countryCode": "IT", "code": "25", "iso31662": "IT-25",
  "level": 1, "geoNameId": 3174618,
  "seed": { /* ... */ }
}
```

```jsonc
// city
{
  "id": "city-3173435", "partitionKey": "IT", "type": "city",
  "name": "Milan", "asciiName": "Milan",
  "alternateNames": ["Milano","Mailand"],
  "countryCode": "IT", "regionCode": "25", "regionName": "Lombardy",
  "adm2Code": "MI", "geoNameId": 3173435,
  "location":  { "type": "Point", "coordinates": [9.18951, 45.46427] },
  "latitude": 45.46427, "longitude": 9.18951,
  "elevation": 122, "population": 1371498,
  "timezone": "Europe/Rome", "featureClass": "P", "featureCode": "PPLA",
  "seed": { /* ... */ }
}
```

### B. `CosmosdbConsole` extended with AAD support

Target account has `disableLocalAuth=true`, so key‑based authentication
returns `401 — Local Authorization is disabled. Use an AAD token to
authorize all requests.`

#### Code Changes

**1. `CosmosdbConsole.csproj`** — added `Azure.Identity` and bumped `System.Linq.Async`:

```xml
<PackageReference Include="Azure.Identity" Version="1.*" />
<!-- System.Linq.Async bumped from 6.* to 7.* (transitive constraint from
     Diginsight.Components.Azure 1.0.0.104) -->
<PackageReference Include="System.Linq.Async" Version="7.*" />
```

**2. `Executor.cs`** — single helper, used everywhere a `CosmosClient` is built:

```csharp
private static CosmosClient CreateCosmosClient(string connectionString, ILogger logger)
{
    var parts = connectionString
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(static x => x.Split('=', 2))
        .Where(static x => x.Length == 2)
        .ToDictionary(static x => x[0].Trim(),
                      static x => x[1].Trim(),
                      StringComparer.OrdinalIgnoreCase);

    string? accountEndpoint = null;
    if (parts.TryGetValue("AccountEndpoint", out var ep))
        accountEndpoint = ep;
    else if (Uri.IsWellFormedUriString(connectionString.Trim(), UriKind.Absolute))
        accountEndpoint = connectionString.Trim();

    bool hasKey = parts.ContainsKey("AccountKey");
    if (!hasKey)
    {
        logger.LogInformation("Using AAD auth (DefaultAzureCredential) for endpoint {endpoint}",
                              accountEndpoint);
        return new CosmosClient(accountEndpoint, new DefaultAzureCredential());
    }
    return new CosmosClient(connectionString);
}
```

`QueryAsync`, `UploadDocumentsJsonAsync` and `DeleteDocumentsFromJsonAsync`
were updated to call `CreateCosmosClient(connectionString, logger)`
instead of `new CosmosClient(connectionString)` directly. The connection
string can now be either a full key‑based connection string **or** a bare
endpoint URL (e.g. `https://<cosmos-account>.documents.azure.com:443/`).

### C. RBAC — Cosmos DB Built‑in Data Contributor

Granted to the signed‑in user so that `DefaultAzureCredential` can write:

| Property | Value |
|---|---|
| Role definition | `Cosmos DB Built-in Data Contributor` |
| Role definition id | `00000000-0000-0000-0000-000000000002` |
| Principal | `<signed-in-user-object-id>` |
| Scope | `/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.DocumentDB/databaseAccounts/<cosmos-account>` |
| Assignment id | `<role-assignment-guid>` |

> Note: this is the **data‑plane** role exposed via
> `az cosmosdb sql role assignment create`. ARM RBAC roles like
> `Cosmos DB Account Reader` do **not** grant data‑plane access.

---

## ⬆️ HOW DATA WAS UPLOADED

`99-upload.ps1` iterates the built JSON files in dependency order
(`continents → countries → regions → cities → addresses`) and shells out to
the local `CosmosdbConsole`:

```powershell
dotnet run --project $consoleProj --no-launch-profile -- `
  uploadjson `
  -f $jsonPath `
  -c $env:COSMOS_CONN `
  -d location -t location `
  -s "_etag,_rid,_self,_ts,_attachments"
```

Notes:

- `-c $env:COSMOS_CONN` is set by `01-connect.ps1`. With `disableLocalAuth=true`
  it is the **endpoint URL only** ⇒ `CreateCosmosClient` selects the AAD branch.
- `-s ...` instructs the console to strip Cosmos system properties on
  upsert (they are also stripped by `NormalizeDocument` along with anything
  starting with `_`).
- `--no-launch-profile` avoids a malformed `launchSettings.json` in the
  console project (separately tracked).
- **Cocona returns the command's `Task<int>` as the process exit code**, so a
  positive exit code means *"N documents upserted"*, not an error. The
  upload script therefore treats `$LASTEXITCODE -lt 0` as failure and
  reports the positive value as upserted count.

### Run output

```
==> Uploading continents.json ...
    continents.json: 7 documents upserted
==> Uploading countries.json ...
    countries.json: 250 documents upserted
==> Uploading regions.json ...
    regions.json: 1183 documents upserted
==> Uploading cities.json ...
    cities.json: 8527 documents upserted
WARNING: Skipping addresses.json (not built yet)
Upload complete.
```

### Verification

```powershell
az cosmosdb sql container show `
   --account-name $account `
   --resource-group $rg `
   --database-name location --name location `
   --query "resource.statistics" -o json
```

```json
[
  {
    "documentCount": 9967,
    "id": "0",
    "partitionKeys": [ ],
    "sizeInKB": 10432
  }
]
```

9 967 = 7 + 250 + 1 183 + 8 527 ✅

---

## 📚 ADDITIONAL INFORMATION

### Files Added

| Path | Purpose |
|---|---|
| [`src/03.01 CosmosDB/seed-locations/scripts/_common.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/_common.ps1) | Shared helpers, constants |
| [`scripts/00-verify-pk.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/00-verify-pk.ps1) | PK sanity check |
| [`scripts/01-connect.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/01-connect.ps1) | Builds `$env:COSMOS_CONN` (key OR endpoint URL) |
| [`scripts/02-download.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/02-download.ps1) | Datasets download/extract |
| [`scripts/03-build-continents.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/03-build-continents.ps1) | Continents (7) |
| [`scripts/04-build-countries.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/04-build-countries.ps1) | Countries (250) |
| [`scripts/05-build-regions.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/05-build-regions.ps1) | European regions (1 183) |
| [`scripts/06-build-cities.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/06-build-cities.ps1) | European cities (8 527) |
| [`scripts/07-build-addresses.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/07-build-addresses.ps1) | Synthetic addresses (optional) |
| [`scripts/99-upload.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/99-upload.ps1) | Upload orchestrator |
| [`01.location-download-plan.md`](./01.location-download-plan.md) | Master plan, schema option matrix |

### Files Modified

| Path | Change |
|---|---|
| [`CosmosdbConsole/CosmosdbConsole.csproj`](../../../03.01%20CosmosDB/CosmosdbConsole/CosmosdbConsole.csproj) | `+Azure.Identity 1.*`; `System.Linq.Async 6.* → 7.*` |
| [`CosmosdbConsole/Executor.cs`](../../../03.01%20CosmosDB/CosmosdbConsole/Executor.cs) | `+CreateCosmosClient` helper; 3 call sites switched to it |

### Idempotency Properties

- IDs are deterministic ⇒ re‑running `99-upload.ps1` upserts in place with
  no duplicates.
- Build scripts only re‑download missing datasets (cached in `raw/`).
- `seed.loadedAt` updates on every rebuild, providing audit trail.

### Performance Notes

- Upload runs sequentially via the upsert path of `CosmosdbConsole`. On the
  default Free‑tier throughput it completed in a few minutes end‑to‑end.
- For large re‑seeds, consider bulk mode (`CosmosClientOptions.AllowBulkExecution = true`)
  or `BulkImporter` — not required at 10 k docs.

### Security Considerations

- ✅ **Key‑less auth**: production‑style AAD path is now the default for
  any Cosmos account with `disableLocalAuth=true`.
- ✅ **Least privilege**: data‑plane RBAC role assigned at account scope,
  not subscription scope; only to the signed‑in user, not a broad group.
- ⚠️ **Data freshness**: GeoNames is a snapshot; re‑run `02-download.ps1`
  periodically if up‑to‑date population/timezone is required.

---

## ✔️ RESOLUTION STATUS

### 🎯 **STATUS: RESOLVED**

**Resolution Date:** 2026-05-24
**Resolved By:** Dario Airoldi (with GitHub Copilot)
**Resolution Type:** Feature implementation + tooling enhancement

### Verification Checklist

- [x] Container partition key verified (`/partitionKey`)
- [x] Datasets downloaded & cached
- [x] Schemas drafted, options compared, **Option B** chosen
- [x] All build scripts authored (ASCII source, idempotent)
- [x] `CosmosdbConsole` extended with `DefaultAzureCredential`
- [x] `Cosmos DB Built-in Data Contributor` granted to signed‑in user
- [x] `99-upload.ps1` executed end‑to‑end with no errors
- [x] Document count verified in Cosmos: **9 967**

### Follow-up Actions

#### Optional (when needed)
- [ ] Run [`07-build-addresses.ps1`](../../../03.01%20CosmosDB/seed-locations/scripts/07-build-addresses.ps1) (and re‑run `99-upload.ps1`) to seed
  ~2 000 synthetic addresses.
- [ ] Extend `05-build-regions.ps1` / `06-build-cities.ps1` to cover other
  continents if needed (today only Europe is filtered in).

#### Deferred (legacy code alignment)
- [ ] Update [`LocationController.cs`](../../../03.01%20CosmosDB/LocationAPI/Controllers/LocationController.cs) — currently queries
  `WHERE c.Type = @type` (PascalCase). New seed data exposes `type`
  (camelCase). Either change the query to `c.type`, or configure the
  Cosmos serializer with `JsonNamingPolicy.CamelCase`, or adopt
  `[JsonPropertyName("type")]` on the legacy models.
- [ ] Fix [`CosmosdbConsole/Properties/launchSettings.json`](../../../03.01%20CosmosDB/CosmosdbConsole/Properties/launchSettings.json)
  (currently bypassed via `--no-launch-profile`).

---

## 🎓 LESSONS LEARNED

### What went well
1. **Schema option matrix up front** — choosing Option B *before* coding
   saved a rewrite later; the country‑sharded model gives both
   single‑partition reads for a country and even data distribution.
2. **Deterministic IDs everywhere** — made the entire pipeline trivially
   idempotent without bookkeeping.
3. **Enrichment over composition** — merging mledoze + GeoNames
   countryInfo gave a much richer country record (population, postal
   regex, neighbours) than either source alone.

### What went wrong (and how it was fixed)
1. **`_seed` provenance got silently dropped** — `NormalizeDocument()`
   strips all `_*` properties unconditionally. Fixed by renaming to
   `seed`. *Lesson:* avoid underscore prefixes for custom metadata when
   the pipeline can rewrite documents.
2. **401 on upload** — `disableLocalAuth=true` blocks key auth. Fixed by
   teaching `CosmosdbConsole` to fall back to `DefaultAzureCredential`
   when the connection string is a bare URL or has no `AccountKey=`.
   *Lesson:* production Cosmos accounts are increasingly key‑less; tools
   must support AAD as a first‑class auth path.
3. **NU1605 package downgrade** — `Diginsight.Components.Azure 1.0.0.104`
   transitively requires `System.Linq.Async >= 7.0.1` but the project
   pinned `6.*`. Fixed by bumping to `7.*` and `dotnet restore --force-evaluate`.
4. **Cocona exit code confusion** — Cocona maps the command method's
   `Task<int>` return to the process exit code, so a "positive" exit code
   means *"N documents upserted"*, not failure. Fixed by treating only
   `$LASTEXITCODE -lt 0` as an error in the orchestrator script.
5. **PS 5.1 + Unicode source** — initial scripts had a few non‑ASCII
   characters that crashed Windows PowerShell parsing. Fixed by
   normalising all script source to ASCII.

---

## 📎 APPENDIX

### A. Reference URLs

- mledoze/countries — <https://github.com/mledoze/countries>
- GeoNames data dumps — <https://download.geonames.org/export/dump/>
- GeoNames licence (CC‑BY 4.0) — <https://creativecommons.org/licenses/by/4.0/>
- Azure Cosmos DB data‑plane RBAC — <https://learn.microsoft.com/azure/cosmos-db/how-to-setup-rbac>
- Azure Cosmos DB `DefaultAzureCredential` quickstart — <https://learn.microsoft.com/azure/cosmos-db/nosql/quickstart-dotnet?tabs=passwordless>
- Cosmos DB GeoJSON / spatial queries — <https://learn.microsoft.com/azure/cosmos-db/nosql/sql-query-geospatial-intro>

### B. CLI reference

Grant the data‑plane role:

```powershell
az cosmosdb sql role assignment create `
    --account-name $account `
    --resource-group $rg `
    --scope "/" `
    --principal-id <object-id-of-the-user> `
    --role-definition-id 00000000-0000-0000-0000-000000000002
```

Verify container statistics:

```powershell
az cosmosdb sql container show `
    --account-name $account `
    --resource-group $rg `
    --database-name location --name location `
    --query "resource.statistics" -o json
```

Sample data‑plane query (camelCase aware):

```sql
SELECT TOP 5 c.id, c.name, c.population, c.partitionKey
FROM c
WHERE c.type = "city"
  AND c.partitionKey = "IT"
ORDER BY c.population DESC
```

---

**Document Version:** 1.0
**Last Updated:** 2026-05-24
**Next Review:** when adding non‑European data or migrating `LocationAPI` to the new schema
