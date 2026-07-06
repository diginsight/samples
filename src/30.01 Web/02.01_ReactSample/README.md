# 02.01 React Sample

A React + TypeScript + Vite single-page application (`ReactApp.Client`) backed by an
ASP.NET Core API (`ReactApp.Api`), demonstrating the same frontend architecture as the
Blazor sample (`01.01_BlazorSample`) but built with the React stack:

- **React 19 + TypeScript + Vite**
- **Fluent UI** (`@fluentui/react-components`)
- **React Router 7** (`react-router-dom`)
- **Axios** with an authentication request interceptor that attaches an MSAL access token
- **MSAL** (`@azure/msal-browser` + `@azure/msal-react`) for Microsoft Entra ID sign-in
- **Diginsight** telemetry + log4net on the API (identical to the Blazor sample API)

The SPA does **not** hardcode its app registration. At startup it fetches the MSAL
configuration (client id / authority / scopes) from the API's anonymous
`GET /api/clientconfig/auth` endpoint — mirroring the Blazor sample's server-driven config.

## Projects

| Project | Description | App registration |
| --- | --- | --- |
| `ReactApp.Api` | Protected Web API (weather forecast) + anonymous client-config endpoint | `samples-testmc-appreg-02` (API) |
| `ReactApp.Client` | React SPA with the "Agent" page showing weather data | `samples-testmc-appreg-01` (SPA client) |

## Running locally (Testmc)

The Entra configuration lives in the private `samples.internal` repo
(`appsettings.Testmc.json`), loaded via `AppsettingsEnvironmentName=Testmc` +
`ExternalConfigurationFolder`, exactly like the Blazor sample.

1. **Start the API** with the `https - Testmc` launch profile:

   ```powershell
   cd "src/30.01 Web/02.01_ReactSample/ReactApp.Api"
   dotnet run --launch-profile "https - Testmc"
   ```

   The API listens on `https://localhost:7262` (and `http://localhost:5062`).

2. **Start the SPA**:

   ```powershell
   cd "src/30.01 Web/02.01_ReactSample/ReactApp.Client"
   npm install
   npm run dev
   ```

   The Vite dev server listens on `http://localhost:5173`. The API base URL is configured
   in `.env.local` (`VITE_API_BASE_URL=https://localhost:7262`).

3. Open `http://localhost:5173`, sign in, and the **Agent** page loads the 5-day forecast
   from the protected `/weatherforecast` endpoint.

### Entra redirect URI

The SPA app registration (`samples-testmc-appreg-01`) must have `http://localhost:5173`
registered as a **Single-page application** redirect URI. Add it under
*App registrations → samples-testmc-appreg-01 → Authentication → Single-page application*
if sign-in fails with `redirect_uri` mismatch. Use `http://localhost:4173` for
`npm run preview`.

## Running locally (no auth)

If you start the API with the plain `https` profile (without the Testmc config), the
`/api/clientconfig/auth` endpoint returns empty values, authentication is disabled, and
the SPA shows a "not configured" notice instead of the sign-in button.

## Architecture notes

- [src/main.tsx](ReactApp.Client/src/main.tsx) — bootstrap: fetch config → create + initialize
  MSAL → process redirect → mount React.
- [src/api/apiClient.ts](ReactApp.Client/src/api/apiClient.ts) — Axios instance with the request
  interceptor that calls `acquireTokenSilent` and attaches the bearer token.
- [src/auth/authConfig.ts](ReactApp.Client/src/auth/authConfig.ts) — builds the MSAL config from the
  server-advertised values and holds the API scopes.
- [ReactApp.Api/Controllers/ClientConfigController.cs](ReactApp.Api/Controllers/ClientConfigController.cs)
  — anonymous endpoint that serves the SPA's MSAL config from the `SpaClientAuth` section.
- [ReactApp.Api/Controllers/WeatherForecastController.cs](ReactApp.Api/Controllers/WeatherForecastController.cs)
  — protected endpoint (`[Authorize]` + `[RequiredScope]`).
