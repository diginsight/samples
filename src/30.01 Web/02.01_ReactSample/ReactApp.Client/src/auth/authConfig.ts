import { type AccountInfo, type Configuration, LogLevel } from "@azure/msal-browser";
import type { ClientAuthConfig } from "../api/types";

// Placeholder client id used when the API did not advertise an app registration
// (e.g. running the plain Development profile without the Testmc configuration).
// MSAL requires a non-empty client id to initialize; auth simply never succeeds.
const NOT_CONFIGURED_CLIENT_ID = "00000000-0000-0000-0000-000000000000";

// Access token scopes required to call the protected API, advertised by the server.
let apiScopes: string[] = [];

export function setApiScopes(scopes: string[]): void {
  apiScopes = scopes;
}

export function getApiScopes(): string[] {
  return apiScopes;
}

/**
 * Builds the MSAL configuration from the values served by the API.
 */
export function buildMsalConfig(cfg: ClientAuthConfig | null): Configuration {
  const redirectUri = window.location.origin;
  return {
    auth: {
      clientId: cfg?.clientId || NOT_CONFIGURED_CLIENT_ID,
      authority: cfg?.authority || "https://login.microsoftonline.com/common",
      redirectUri,
      postLogoutRedirectUri: redirectUri,
    },
    cache: {
      cacheLocation: "sessionStorage",
    },
    system: {
      loggerOptions: {
        logLevel: LogLevel.Warning,
        loggerCallback: (_level, message) => {
          console.debug(message);
        },
      },
    },
  };
}

/**
 * Scopes requested at sign-in. Includes the API scope (when configured) so the user
 * consents once and a valid access token for the API audience is available afterwards.
 */
export function getLoginRequest() {
  return { scopes: ["openid", "profile", ...apiScopes] };
}

/**
 * Extracts the signed-in user's display name from an MSAL account.
 */
export function getUserName(account: AccountInfo | null | undefined): string {
  return account?.name || account?.username || "";
}
