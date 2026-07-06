import { PublicClientApplication, type Configuration } from "@azure/msal-browser";

// The MSAL instance is created during bootstrap (main.tsx) once the API has advertised
// the app registration, so it cannot be a module-level constant like in a static-config app.
let instance: PublicClientApplication | null = null;

export function createMsalInstance(config: Configuration): PublicClientApplication {
  instance = new PublicClientApplication(config);
  return instance;
}

export function getMsalInstance(): PublicClientApplication {
  if (!instance) {
    throw new Error("MSAL instance not initialized. Call createMsalInstance() during bootstrap.");
  }
  return instance;
}
