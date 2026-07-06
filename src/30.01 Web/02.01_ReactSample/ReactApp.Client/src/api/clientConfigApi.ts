import axios from "axios";
import type { ClientAuthConfig } from "./types";

// Virtual path the app is hosted under (e.g. "/reactapp"), injected by ReactApp.Api into the
// served index.html. Falls back to VITE_BASE_PATH (Vite dev server) then "" (root).
const injectedBase =
  typeof window !== "undefined"
    ? (window as unknown as { __BASE_PATH__?: string }).__BASE_PATH__
    : undefined;

export const basePath: string = injectedBase ?? import.meta.env.VITE_BASE_PATH ?? "";

// The API is same-origin under "{basePath}/api".
export const apiBaseUrl: string = `${basePath}/api`;

/**
 * Fetches the MSAL bootstrap configuration (client id / authority / scopes) from the API.
 * Uses a bare axios call (no auth interceptor) because it runs before MSAL is initialized.
 * Returns null when the API is unreachable so the app can render a "not configured" state.
 */
export async function fetchClientAuthConfig(): Promise<ClientAuthConfig | null> {
  try {
    const response = await axios.get<ClientAuthConfig>(
      `${apiBaseUrl}/clientconfig/auth`,
      { timeout: 10000 },
    );
    return response.data;
  } catch {
    return null;
  }
}
