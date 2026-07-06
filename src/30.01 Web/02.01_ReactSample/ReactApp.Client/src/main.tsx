import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App.tsx";
import { fetchClientAuthConfig } from "./api/clientConfigApi.ts";
import { buildMsalConfig, setApiScopes } from "./auth/authConfig.ts";
import { createMsalInstance } from "./auth/msalInstance.ts";

/**
 * Bootstrap sequence (mirrors the Blazor sample's server-driven MSAL config):
 *  1. Fetch the app registration (client id / authority / scopes) from ReactApp.Api.
 *  2. Create and initialize the MSAL instance from that configuration.
 *  3. Process any redirect response and set the active account.
 *  4. Mount React.
 */
async function bootstrap() {
  const authConfig = await fetchClientAuthConfig();
  const isAuthConfigured = Boolean(authConfig?.clientId);

  const msalInstance = createMsalInstance(buildMsalConfig(authConfig));

  const scopes = (authConfig?.scopes ?? "")
    .split(" ")
    .map((s) => s.trim())
    .filter(Boolean);
  setApiScopes(scopes);

  // MSAL v3+ requires initialize() before any interaction.
  await msalInstance.initialize();

  const redirectResult = await msalInstance.handleRedirectPromise();
  if (redirectResult?.account) {
    msalInstance.setActiveAccount(redirectResult.account);
  } else {
    const accounts = msalInstance.getAllAccounts();
    if (accounts.length > 0) {
      msalInstance.setActiveAccount(accounts[0]);
    }
  }

  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <App instance={msalInstance} isAuthConfigured={isAuthConfigured} />
    </StrictMode>,
  );
}

bootstrap().catch((error) => {
  console.error("MSAL bootstrap error", error);
});
