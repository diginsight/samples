import axios from "axios";
import { getMsalInstance } from "../auth/msalInstance";
import { getApiScopes } from "../auth/authConfig";
import { apiBaseUrl } from "./clientConfigApi";

const apiClient = axios.create({
  baseURL: apiBaseUrl,
});

// Request interceptor: attach an access token acquired silently for the API scopes.
apiClient.interceptors.request.use(async (config) => {
  const msalInstance = getMsalInstance();
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0];
  const scopes = getApiScopes();

  if (account && scopes.length > 0) {
    try {
      const response = await msalInstance.acquireTokenSilent({ scopes, account });
      config.headers.Authorization = `Bearer ${response.accessToken}`;
    } catch {
      // Silent acquisition failed (e.g. expired session / no consent) — fall back to redirect.
      await msalInstance.acquireTokenRedirect({ scopes, account });
    }
  }
  return config;
});

export default apiClient;
