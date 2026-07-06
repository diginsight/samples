# PLAN: Deploy multiple Web/API samples to a single App Service

**Date:** 2026-07-06
**Author:** Dario Airoldi
**Status:** Draft / Feasibility
**Component:** 30.01 Web (01.01_BlazorSample, 02.01_ReactSample, …)
**Target App Service:** `samples-testmc-app-itn-01.azurewebsites.net` (Testmc)

---

## 📋 Table of Contents

1. [🎯 Goal](#-goal)
2. [🧩 What each sample actually is](#-what-each-sample-actually-is)
3. [✅ Feasibility verdict](#-feasibility-verdict)
4. [🗂️ Target layout on the single App Service](#️-target-layout-on-the-single-app-service)
5. [🔀 Hosting options](#-hosting-options)
6. [🔧 Per-app changes required](#-per-app-changes-required)
7. [🔐 Identity / Entra changes](#-identity--entra-changes)
8. [🚦 Open questions / decisions needed](#-open-questions--decisions-needed)
9. [📌 Not in scope / caveats](#-not-in-scope--caveats)

---

## 🎯 GOAL

Deploy the Web/API samples to the **single** shared App Service
`samples-testmc-app-itn-01`, each under its own path folder, with every sample running
**independently** (its own client + its own API, no cross-interference):

| Path prefix              | Sample              | Deployables                          |
| ------------------------ | ------------------- | ------------------------------------ |
| `/01.01_BlazorSample`    | 01.01_BlazorSample  | `BlazorApp.Client` + `BlazorApp.Api` |
| `/02.01_ReactSample`     | 02.01_ReactSample   | `ReactApp.Client` + `ReactApp.Api`   |
| `/…` (future)            | …                   | …                                    |

---

## 🧩 WHAT EACH SAMPLE ACTUALLY IS

Each sample is **two independent deployable units**, decoupled today via CORS:

- **A protected ASP.NET Core API** (.NET 10) — JWT bearer / Microsoft.Identity.Web,
  serves a protected endpoint (`/weatherforecast`) + an anonymous config endpoint
  (`/api/clientconfig/auth`) that advertises the SPA's MSAL settings.
- **A static SPA client** that calls the API with an MSAL access token:
  - `BlazorApp.Client` → **Blazor WebAssembly** (static files, MSAL callback path
    `/authentication/login-callback`, base href in `wwwroot/index.html`).
  - `ReactApp.Client` → **React 19 + Vite** SPA (static files, `BrowserRouter`,
    MSAL `redirectUri = window.location.origin`, API base from `VITE_API_BASE_URL`).

Because the SPA and its API are separate origins today, they communicate over CORS.
When co-hosted under the **same host** but different paths, they become **same-origin**,
so CORS is no longer strictly required for the client→API call.

---

## ✅ FEASIBILITY VERDICT

**Feasible — yes.** The proposed one-folder-per-sample layout is exactly the right model.
The clean, low-refactor path is a **Windows App Service using Virtual Applications**, where
each API is registered as its own *Application* and therefore runs as an **isolated
ASP.NET Core process** (own ANCM handler / worker context). That satisfies "everyone works
on its own" at the process level, not just by URL path.

> **Pivotal factor — OS of the App Service.**
> - **Windows App Service** → native multi-app hosting via *Path mappings → Virtual
>   applications and directories*. ✅ Recommended.
> - **Linux App Service** → **no** virtual applications; one container / one startup
>   command only. Would require a YARP reverse proxy or a single aggregator app (more
>   refactor, tighter coupling). ⚠️ Confirm before committing to the simple path.

Action: confirm `samples-testmc-app-itn-01` is **Windows** (expected, given the `.NET`
stack). If Linux, switch to [Option C](#-hosting-options).

---

## 🗂️ TARGET LAYOUT ON THE SINGLE APP SERVICE

`site\wwwroot` on the App Service:

```
wwwroot/
  index.html                       ← optional landing page linking to each sample
  01.01_BlazorSample/              ← Blazor WASM static (Virtual dir, base href = /01.01_BlazorSample/)
    api/                           ← BlazorApp.Api  (Virtual APPLICATION → own process)
  02.01_ReactSample/               ← React/Vite static (Virtual dir, base = /02.01_ReactSample/)
    api/                           ← ReactApp.Api   (Virtual APPLICATION → own process)
```

Resulting URLs:

- `https://…/01.01_BlazorSample/`                → Blazor SPA
- `https://…/01.01_BlazorSample/api/weatherforecast` → Blazor API
- `https://…/02.01_ReactSample/`                 → React SPA
- `https://…/02.01_ReactSample/api/weatherforecast`  → React API

---

## 🔀 HOSTING OPTIONS

### Option A — Windows App Service + Virtual Applications ✅ (recommended)

- Mark each `…/api` folder as an **Application** under *Configuration → Path mappings*.
- Each API gets its own `web.config` (ANCM) from `dotnet publish`; ANCM sets the request
  **path base** automatically, so `/02.01_ReactSample/api/*` routes correctly with **no**
  `UsePathBase` code change needed.
- Static SPA folders need a `web.config` with a SPA fallback rewrite to their `index.html`
  (Blazor WASM publish emits one; Vite needs one added).
- **Pros:** true process isolation per API, minimal code change, no proxy.
- **Cons:** Windows-only; SPA base-path changes still required (see below).

### Option B — Aggregator ASP.NET Core app (Windows or Linux)

- One host app that `UseStaticFiles` for each SPA under its path and hosts all API
  controllers under `/{sample}/api/...`.
- **Pros:** OS-agnostic, single deployment unit.
- **Cons:** all APIs share one process (no isolation), merges projects — biggest refactor.

### Option C — YARP reverse proxy (Linux fallback)

- A single front app (YARP) routes `/{sample}/*` to per-sample handlers; static served by
  the proxy.
- **Pros:** works on Linux, keeps APIs logically separate.
- **Cons:** extra moving part; still one process/container.

---

## 🔧 PER-APP CHANGES REQUIRED

These are needed regardless of hosting option because the apps move from `/` to a sub-path.

### Blazor sample (`01.01_BlazorSample`)

- `BlazorApp.Client/wwwroot/index.html` → `<base href="/01.01_BlazorSample/" />`
  (currently `"/"`). Also verify the service-worker scope.
- `BlazorApp.Client/wwwroot/appsettings.json` → `ServerConfig:BaseUrl` pointing at
  `…/01.01_BlazorSample/api` (relative or absolute).
- `BlazorApp.Api` → publish as a virtual application; set
  `Cors:AllowedOrigins` to include the deployed host (or drop CORS since it's same-origin).

### React sample (`02.01_ReactSample`)

- `ReactApp.Client/vite.config.ts` → add `base: '/02.01_ReactSample/'`.
- `src/App.tsx` → `<BrowserRouter basename="/02.01_ReactSample">`.
- MSAL `redirectUri` in `src/auth/authConfig.ts` → `window.location.origin + '/02.01_ReactSample/'`
  (currently bare origin) and matching `postLogoutRedirectUri`.
- API base URL (`VITE_API_BASE_URL` / build-time) → `…/02.01_ReactSample/api`.
- Add a `web.config` (SPA fallback) for the static folder on Windows App Service.
- `ReactApp.Api` → publish as a virtual application; align `Cors:AllowedOrigins`.

---

## 🔐 IDENTITY / ENTRA CHANGES

Each SPA sign-in redirect URI is host+path specific and must be registered on the **SPA**
app registration `samples-testmc-appreg-01` (see sibling issue `20260705.01`):

- Blazor: `https://samples-testmc-app-itn-01.azurewebsites.net/01.01_BlazorSample/authentication/login-callback`
- React (SPA type): `https://samples-testmc-app-itn-01.azurewebsites.net/02.01_ReactSample/`

The API app registration `samples-testmc-appreg-02` (audience/scopes) is unchanged.
API config (`AzureAd`, Key Vault, Application Insights connection string) comes from the
App Service application settings / `appsettings.Testmc.json` in the private
`samples.internal` repo.

---

## 🚦 OPEN QUESTIONS / DECISIONS NEEDED

1. **OS of `samples-testmc-app-itn-01`** — Windows (Option A) or Linux (Option B/C)?
2. **Deployment mechanism** — zip deploy per virtual app, single zip of the whole
   `wwwroot`, or CI/CD (GitHub Actions / azd)?
3. **Landing page** at `/` — needed, or is a bare root acceptable?
4. **CORS** — keep (belt-and-braces) or drop now that client+API are same-origin?
5. **Scope** — only `01.01` + `02.01` for now, or also `01.02_BlazorSample`
   (Aspire — see caveat)?

---

## 📌 NOT IN SCOPE / CAVEATS

- **`01.02_BlazorSample` (BlazorAspireApp)** is a .NET Aspire orchestration app. Aspire's
  AppHost model does **not** map cleanly to a shared single App Service path folder and is
  **excluded** from this plan unless explicitly required.
- Blazor WASM under a sub-path requires the correct `<base href>`; getting it wrong breaks
  `_framework/*` asset loading and the service worker.
- SPA deep-link refresh (e.g. `/02.01_ReactSample/agent`) requires the fallback rewrite to
  each sample's own `index.html`.
