// DTOs returned by ReactApp.Api. ASP.NET Core serializes to camelCase by default.

export interface WeatherForecast {
  date: string;
  temperatureC: number;
  temperatureF: number;
  summary: string | null;
}

/**
 * Authentication configuration served by the API's anonymous
 * /api/clientconfig/auth endpoint, used to bootstrap MSAL in the browser.
 */
export interface ClientAuthConfig {
  clientId: string | null;
  authority: string | null;
  validateAuthority: boolean | null;
  scopes: string | null;
}
