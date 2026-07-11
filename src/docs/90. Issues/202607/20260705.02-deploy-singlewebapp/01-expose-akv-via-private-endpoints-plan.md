# PLAN: Expose Azure Key Vault (and Storage / DBs) via Private Endpoints

**Date:** 2026-07-08
**Author:** Dario Airoldi
**Status:** In progress — ✅ Phase 0 (B1 + Always On), ✅ **Phase 1 (Key Vault PE)**, ✅ **Phase 2 (Storage / Cosmos / App Config PEs)** done (2026-07-08). Remaining: Phase 3 (Bicep) + optional App Insights AMPLS.
**Environment:** `samples-testmc-rg-itn-01` (Italy North); networking in `network-testmc-rg-itn-01`

> **Model chosen: B (Deny + dev IP).** KV keeps `publicNetworkAccess=Enabled`,
> `defaultAction=Deny`, with **only the dev IP** allowed; the App Service reaches KV
> exclusively over the private endpoint. Address space `10.20.0.0/16` confirmed. IaC:
> `az` now, Bicep to be backfilled.
**Trigger:** The App Service kept losing Key Vault access whenever the vault firewall
IP allow‑list was reset, causing `HTTP 502.5` (the app hard‑crashes at startup when it
cannot read Key Vault). Private endpoints remove the dependency on public IP allow‑listing.

---

## 📋 Table of Contents

1. [🎯 Goal](#-goal)
2. [🩺 Why this is needed](#-why-this-is-needed)
3. [🚧 Hard prerequisite — App Service Plan SKU](#-hard-prerequisite--app-service-plan-sku)
4. [🧭 Current inventory (Italy North)](#-current-inventory-italy-north)
5. [🏗️ Target architecture](#️-target-architecture)
6. [🌐 Private DNS zones & subresources](#-private-dns-zones--subresources)
7. [🪜 Implementation phases](#-implementation-phases)
8. [💻 Local‑developer impact](#-local-developer-impact)
9. [✅ Validation](#-validation)
10. [↩️ Rollback](#️-rollback)
11. [💰 Cost estimate](#-cost-estimate)
12. [🚦 Decisions needed before we start](#-decisions-needed-before-we-start)

---

## 🎯 GOAL

Give the App Service a **private network path** to its backing services so that access
**does not depend on a public firewall IP allow‑list**. Start with **Key Vault** (the thing
that broke), then apply the same VNet to **Storage**, **Cosmos DB**, and **App Configuration**.

Outcome:
- App Service reaches Key Vault (and friends) over a private IP inside a VNet.
- Public network access on those resources can be **Disabled** (most secure) or kept
  Deny‑by‑default with only a dev exception — the App Service no longer relies on it.
- No more `502.5` when someone edits the vault firewall.

---

## 🩺 WHY THIS IS NEEDED

The app loads Key Vault **at startup** (`Diginsight ConfigureAppConfiguration2` →
`AddAzureKeyVault`). If the vault is unreachable, the config provider throws and the
process exits → ANCM returns **`HTTP 502.5 – Out‑Of‑Process Startup Failure`**.

Today the App Service reaches the vault over the **public** endpoint, allowed only because
its ~56 outbound IPs are on the vault firewall. That list is **fragile**: the portal action
"allow access from this network" **replaces** the whole list with a single dev IP, silently
removing the App Service IPs — which is exactly what caused the last outage.

> A private endpoint makes the App Service→Vault path independent of that allow‑list.

---

## 🚧 HARD PREREQUISITE — App Service Plan SKU

| Item | Status | Required for VNet integration |
| --- | --- | --- |
| Plan `samples-testmc-asp-01` | ✅ **B1 (Basic)** — upgraded 2026-07-08 (was F1) | **B1 (Basic)** or higher |

**Regional VNet integration is not available on Free (`F1`) or Shared (`D1`) tiers.** Without
VNet integration the App Service cannot route to a private endpoint. ✅ **Done:** the plan was
upgraded to `B1` and **Always On** enabled; both apps verified returning `200` afterwards.

> ⚠️ This is a **cost** and **approval** decision — see [Cost](#-cost-estimate) and
> [Decisions](#-decisions-needed-before-we-start).

---

## 🧭 CURRENT INVENTORY (Italy North)

Resources in `samples-testmc-rg-itn-01` and whether they're in scope:

| Resource | Name | Private‑endpoint candidate? |
| --- | --- | --- |
| Key Vault | `samples-testmc-kv-itn-01` | ✅ **Phase 1 (priority)** |
| App Configuration | `samples-testmc-apc-itn-01`, `samples-testmc-apc-itn-03` | ✅ Phase 2 |
| Storage account | `samplestmcstitn01` | ✅ Phase 2 (blob/table/queue/file) |
| Cosmos DB | `samples-testmc-cdb-itn-01`, `samples-testmc-cdb-itn-02` | ✅ Phase 2 (SQL API) |
| App Insights | `samples-testmc-ai-itn-01` | ⚠️ Optional (needs AMPLS — separate effort) |
| Log Analytics | `…-samples-testmc-rg-it-ITN` | ⚠️ Optional (AMPLS) |
| Event Grid system topic | `samplestmcstitn01-…` | ➖ Follows the storage account |
| App Service Plan | `samples-testmc-asp-01` (**F1**) | 🚧 must upgrade (see above) |
| App Service | `samples-testmc-app-itn-01` | 🔌 gets **VNet integration** (outbound) |

**No VNet exists** in the RG yet — one must be created.

---

## 🏗️ TARGET ARCHITECTURE

```
                         VNet: samples-testmc-vnet-itn-01 (10.20.0.0/16)
   ┌──────────────────────────────────────────────────────────────────────┐
   │  subnet: snet-appsvc-integration (10.20.1.0/24)                        │
   │     delegated to Microsoft.Web/serverFarms                            │
   │     └── App Service regional VNet integration (OUTBOUND) ─────────┐   │
   │                                                                   │   │
   │  subnet: snet-private-endpoints (10.20.2.0/24)                    │   │
   │     ├── PE → Key Vault        (privatelink.vaultcore.azure.net)   │   │
   │     ├── PE → Storage blob/... (privatelink.blob.core.windows.net) │   │
   │     ├── PE → Cosmos DB        (privatelink.documents.azure.com)   │   │
   │     └── PE → App Config       (privatelink.azconfig.io)           │   │
   └──────────────────────────────────────────────────────────────────┼───┘
                                                                        │
   Private DNS zones (linked to the VNet) resolve each FQDN ───────────┘
   to the private‑endpoint IP, so the App Service connects privately.
```

Key mechanics:
- **Regional VNet integration** puts the App Service's **outbound** traffic into
  `snet-appsvc-integration`.
- **`WEBSITE_VNET_ROUTE_ALL=1`** (or `vnetRouteAllEnabled=true`) forces *all* outbound —
  including Key Vault — through the VNet so it can reach the private endpoints.
- **Private DNS zones linked to the VNet** make e.g.
  `samples-testmc-kv-itn-01.vault.azure.net` resolve to the private IP (via the
  `privatelink.*` CNAME) instead of the public IP.

---

## 🌐 PRIVATE DNS ZONES & SUBRESOURCES

| Service | Private DNS zone | PE group id(s) |
| --- | --- | --- |
| Key Vault | `privatelink.vaultcore.azure.net` | `vault` |
| App Configuration | `privatelink.azconfig.io` | `configurationStores` |
| Storage | `privatelink.blob.core.windows.net` (+ `table`, `queue`, `file` as used) | `blob`, `table`, `queue`, `file` |
| Cosmos DB (SQL API) | `privatelink.documents.azure.com` | `Sql` |

> Each **sub‑resource** you use needs its own private endpoint + matching zone (e.g. if the
> storage account is used for blob *and* table, create a PE for each).

---

## 🪜 IMPLEMENTATION PHASES

### Phase 0 — Upgrade the plan (prerequisite) — ✅ DONE (2026-07-08)
```bash
az appservice plan update -g samples-testmc-rg-itn-01 -n samples-testmc-asp-01 --sku B1
az webapp config set -g samples-testmc-rg-itn-01 -n samples-testmc-app-itn-01 --always-on true
```
> ✅ Applied: plan `samples-testmc-asp-01` = **B1/Basic**, `alwaysOn=true`; `…/blazorapp/api` and
> `…/reactapp/api` both returned `200` after the restart.

### Phase 1 — Key Vault private endpoint (the fix) — ✅ DONE (2026-07-08)

> ✅ **Applied & verified.** Actual resources created (names differ from the illustrative
> commands below — a dedicated **networking RG** was used):
>
> | Item | Actual name |
> | --- | --- |
> | Networking RG | `network-testmc-rg-itn-01` |
> | VNet | `network-testmc-vnet-itn-01` (`10.20.0.0/16`) |
> | PE subnet | `network-pe-testmc-snet-itn-01` (`10.20.0.0/24`, PE policies disabled) |
> | App Service integration subnet | `network-samples-testmc-snet-itn-01` (`10.20.1.0/24`, delegated `Microsoft.Web/serverFarms`) |
> | aicm integration subnet (reserved) | `network-aicm-testmc-snet-itn-01` (`10.20.2.0/24`, delegated — app not yet integrated) |
> | KV private endpoint | `pe-kv-itn-01` → group `vault`, private IP **`10.20.0.4`** |
> | Private DNS zones (all linked to the VNet) | `privatelink.vaultcore.azure.net`, `privatelink.azconfig.io`, `privatelink.documents.azure.com`, `privatelink.blob.core.windows.net`, `privatelink.table.core.windows.net` |
>
> - App Service regional VNet integration attached to `network-samples-testmc-snet-itn-01`;
>   `WEBSITE_VNET_ROUTE_ALL=1`.
> - DNS A‑record `samples-testmc-kv-itn-01` → `10.20.0.4` created in the vault zone.
> - KV firewall trimmed to **`Deny` + dev IP `79.21.157.59/32` only** (56 App Service outbound
>   IPs removed). After restart, `…/blazorapp/api/clientconfig/auth` and
>   `…/reactapp/api/clientconfig/auth` both return **`200`** — proving the App Service reaches
>   KV solely through the private endpoint.
>
> The illustrative commands below are kept for reference / Bicep backfill.

1. **Create the VNet + subnets**
   ```bash
   az network vnet create -g samples-testmc-rg-itn-01 -n samples-testmc-vnet-itn-01 \
     -l italynorth --address-prefixes 10.20.0.0/16 \
     --subnet-name snet-private-endpoints --subnet-prefixes 10.20.2.0/24
   az network vnet subnet create -g samples-testmc-rg-itn-01 \
     --vnet-name samples-testmc-vnet-itn-01 -n snet-appsvc-integration \
     --address-prefixes 10.20.1.0/24 \
     --delegations Microsoft.Web/serverFarms
   # PE subnet must disable PE network policies (older CLIs):
   az network vnet subnet update -g samples-testmc-rg-itn-01 \
     --vnet-name samples-testmc-vnet-itn-01 -n snet-private-endpoints \
     --disable-private-endpoint-network-policies true
   ```
2. **App Service regional VNet integration + route‑all**
   ```bash
   az webapp vnet-integration add -g samples-testmc-rg-itn-01 -n samples-testmc-app-itn-01 \
     --vnet samples-testmc-vnet-itn-01 --subnet snet-appsvc-integration
   az webapp config appsettings set -g samples-testmc-rg-itn-01 -n samples-testmc-app-itn-01 \
     --settings WEBSITE_VNET_ROUTE_ALL=1
   ```
3. **Private DNS zone + link**
   ```bash
   az network private-dns zone create -g samples-testmc-rg-itn-01 -n privatelink.vaultcore.azure.net
   az network private-dns link vnet create -g samples-testmc-rg-itn-01 \
     -n kv-dns-link -z privatelink.vaultcore.azure.net \
     -v samples-testmc-vnet-itn-01 --registration-enabled false
   ```
4. **Private endpoint for the vault (+ auto DNS zone group)**
   ```bash
   KVID=$(az keyvault show -n samples-testmc-kv-itn-01 --query id -o tsv)
   az network private-endpoint create -g samples-testmc-rg-itn-01 -n pe-kv-itn-01 \
     -l italynorth --vnet-name samples-testmc-vnet-itn-01 --subnet snet-private-endpoints \
     --private-connection-resource-id $KVID --group-id vault --connection-name kvconn
   az network private-endpoint dns-zone-group create -g samples-testmc-rg-itn-01 \
     --endpoint-name pe-kv-itn-01 -n kv-zg \
     --private-dns-zone privatelink.vaultcore.azure.net --zone-name vault
   ```
5. **Lock down the vault** (public access no longer needed by the App Service)
   ```bash
   # Option A (most secure): fully private
   az keyvault update -n samples-testmc-kv-itn-01 --public-network-access Disabled
   # Option B (keep a dev exception): leave Enabled + defaultAction Deny + dev IP only
   ```
6. **Restart & verify** (see [Validation](#-validation)).

### Phase 2 — Storage, Cosmos, App Configuration — ✅ DONE (2026-07-08)

> ✅ **Applied & verified.** Six private endpoints created in the PE subnet, each with a DNS
> zone group; all FQDNs now resolve to private IPs in the VNet; the deployed app still returns
> `200`. **The deployed `blazorapp`/`reactapp` don't use these services** — this is hardening /
> enabling other apps.
>
> | Resource | Private endpoint | Private IP | Public access (Model B) |
> | --- | --- | --- | --- |
> | Storage `samplestmcstitn01` blob | `pe-st-blob-itn-01` | `10.20.0.5` | `Enabled` + `Deny` + dev IP (already) |
> | Storage `samplestmcstitn01` table | `pe-st-table-itn-01` | `10.20.0.6` | (same account) |
> | Cosmos `samples-testmc-cdb-itn-01` | `pe-cdb01-sql-itn-01` | `10.20.0.7` (+ `.8` regional) | set `Enabled` + dev IP `79.21.157.59` |
> | Cosmos `samples-testmc-cdb-itn-02` | `pe-cdb02-sql-itn-01` | `10.20.0.9` (+ `.10` regional) | set `Enabled` + dev IP `79.21.157.59` |
> | App Config `samples-testmc-apc-itn-01` (developer) | `pe-apc01-itn-01` | `10.20.0.11` | public (App Config has **no IP allow‑list**) |
> | App Config `samples-testmc-apc-itn-03` (free → **developer**) | `pe-apc03-itn-01` | `10.20.0.12` | public (SKU upgraded to enable PE) |
>
> Notes:
> - **Cosmos** was already `publicNetworkAccess=Disabled`; per the chosen model it was set to
>   `Enabled` + dev‑IP filter (Model B) so the dev workstation keeps SDK access while the app
>   uses the private endpoint.
> - **App Config has no IP firewall** (only public on/off + PE). Public access is left `Enabled`
>   so the dev machine keeps working; the PE provides the private path. `apc-itn-03` was on the
>   **free** SKU (no PE support) and was upgraded to **developer** to enable its PE.
> - **Storage** was already `Deny` + dev IP; only the PEs (blob + table) were added.

The illustrative per‑resource commands are the same shape as Phase 1 (zone + group id from the
[table above](#-private-dns-zones--subresources)), reusing the same VNet / PE subnet.

### Phase 2b — aicm app (Key Vault + SQL) — ✅ DONE (2026-07-08)

> ✅ **Applied & verified.** The `aicm-testmc-app-itn-01` app (RG `aicm-testmc-rg-itn-01`) and its
> backing Key Vault + SQL server were brought onto the same VNet / PE subnet.
>
> | Resource | Private endpoint | Private IP | Notes |
> | --- | --- | --- | --- |
> | Key Vault `aicm-testmc-kv-itn-01` | `pe-aicm-kv-itn-01` | `10.20.0.13` | KV was `Enabled` + `Deny` + 1 IP (unchanged) |
> | SQL server `aicm-testmc-sql-itn-01` | `pe-aicm-sql-itn-01` | `10.20.0.14` | was already `publicNetworkAccess=Disabled` with **no PE** → previously unreachable; PE now provides the private path |
>
> - New private DNS zone `privatelink.database.windows.net` (group `sqlServer`) created + linked to the VNet.
> - `aicm-testmc-app-itn-01` given **regional VNet integration** into `network-aicm-testmc-snet-itn-01`
>   + `WEBSITE_VNET_ROUTE_ALL=1`; app returns `200` after restart.
> - The app's `serverFarmId` reads `null` via ARM (display quirk) but VNet integration succeeded, so
>   its plan tier supports it.

### Phase 2c — livequiz Cosmos DB — ⚠️ PARTIAL (2026-07-08)

> ✅ **Private endpoint created.** `livequiz-testmc-cdb-itn-01` (RG `livequiz-testmc-rg-itn-01`,
> SQL API serverless) was already `publicNetworkAccess=Disabled` with **no PE** → previously
> unreachable. Added:
>
> | Resource | Private endpoint | Private IP |
> | --- | --- | --- |
> | Cosmos `livequiz-testmc-cdb-itn-01` | `pe-livequiz-cdb01-sql-itn-01` | `10.20.0.15` (+ `.16` regional) |
>
> Reused the existing `privatelink.documents.azure.com` zone.
>
> ⏳ **App side NOT done (needs a decision).** The two livequiz apps
> (`livequiz-testmc-app-itn-01`, `livequiz-testmc-app-itn-02`) share plan
> `livequiz-testmc-plan-linux-itn-01` which is **F1 (Free) Linux** — regional VNet integration is
> **not supported** on Free. To let the apps reach the Cosmos PE:
> 1. upgrade the plan **F1 → B1 (Linux Basic)** (~€13/mo, lifts both apps),
> 2. add a delegated subnet `network-livequiz-testmc-snet-itn-01` to the VNet,
> 3. VNet-integrate both apps + `WEBSITE_VNET_ROUTE_ALL=1`.

### Phase 3 — Codify in Bicep (recommended)
`samples.internal` already uses Bicep. Move the VNet, subnets, private DNS zones, links,
private endpoints, and the App Service `virtualNetworkSubnetId` into a Bicep module
(AVM: `avm/res/network/virtual-network`, `avm/res/network/private-endpoint`,
`avm/res/network/private-dns-zone`) so this is reproducible and not click‑ops.

---

## 💻 LOCAL‑DEVELOPER IMPACT

With the vault made **private** (Option A), a developer workstation **cannot** reach it —
which is fine, because local dev doesn't need real Key Vault secrets. Options:

- **A. Local dev skips Key Vault (recommended).** Clear `AzureKeyVault:Uri` for local runs
  (e.g. in `appsettings.Testmc.local.json`). *Note:* today the Diginsight loader reads the
  vault URI from the external `appsettings.Testmc.json` **before** the `.local.json` override
  is applied, so the override is currently **inert** — this needs a small loader/order fix to
  work. Alternatively use a plain (non‑Testmc) profile locally.
- **B. Keep a dev firewall exception** (Option B in Phase 1.5): vault stays public with
  `defaultAction: Deny` + your dev IP; the App Service uses the private endpoint. Least
  disruptive to current local dev, but keeps the (now non‑critical) IP list.
- **C. Point‑to‑Site VPN / Bastion** into the VNet for developers who need real vault access.

> Recommendation: **Option B now** (keeps everyone working, makes the App Service robust),
> then move to **Option A** once the loader can reliably skip the vault locally.

---

## ✅ VALIDATION

1. **DNS resolves to a private IP** from inside the App Service (Kudu → SCM console):
   ```bash
   nameresolver samples-testmc-kv-itn-01.vault.azure.net
   # expect 10.20.2.x (private), not a public IP
   ```
2. **App starts** — `https://…/blazorapp/api/clientconfig/auth` and `…/reactapp/…` return **200**.
3. **Public path blocked** (Option A) — from a random public host the vault data plane is
   unreachable; the App Service still works.
4. No `502.5` after `az webapp restart`.

---

## ↩️ ROLLBACK

- Re‑enable public access: `az keyvault update -n … --public-network-access Enabled` and
  re‑add the App Service outbound IPs (the current working state).
- Remove VNet integration: `az webapp vnet-integration remove …`.
- Private endpoints / VNet / DNS zones can be deleted independently; deleting them reverts to
  public access.
- Downgrade the plan back to a lower SKU if the upgrade is not kept.

---

## 💰 COST ESTIMATE (rough, Italy North, monthly)

| Item | ~Cost |
| --- | --- |
| App Service Plan **B1** (from F1/free) | ~€13–15 / mo |
| Private endpoint | ~€6–7 / mo each + minimal data processing |
| Private DNS zone | ~€0.45 / mo per zone + tiny query cost |
| VNet | free |

For KV only: plan upgrade + 1 PE + 1 DNS zone ≈ **~€20/mo**. Full set (KV, storage×N,
cosmos×2, appconfig×2) adds one PE + zone per resource/subresource.

---

## 🚦 DECISIONS NEEDED BEFORE WE START

1. ✅ **Approved & done (2026-07-08)** — plan upgraded `F1 → B1`, Always On enabled, apps verified `200`.
2. ✅ **Done** — KV + Storage + Cosmos + App Config all have private endpoints (Phase 1 + Phase 2 complete).
3. ✅ **Decided — Model B (Deny + dev IP).** KV stays `Enabled` + `defaultAction=Deny` + dev IP only; App Service uses the private endpoint.
4. ✅ **Confirmed** — `10.20.0.0/16` is fine.
5. ✅ **Decided** — `az` now; Bicep backfill later.
6. ⏳ **Open** — App Insights/Log Analytics private (AMPLS) still to decide; left public for now.
