import axios from "axios";
import type { ClientAuthConfig } from "./types";

export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7262";

/**
 * Fetches the MSAL bootstrap configuration (client id / authority / scopes) from the API.
 * Uses a bare axios call (no auth interceptor) because it runs before MSAL is initialized.
 * Returns null when the API is unreachable so the app can render a "not configured" state.
 */
export async function fetchClientAuthConfig(): Promise<ClientAuthConfig | null> {
  try {
    const response = await axios.get<ClientAuthConfig>(
      `${apiBaseUrl}/api/clientconfig/auth`,
      { timeout: 10000 },
    );
    return response.data;
  } catch {
    return null;
  }
}
