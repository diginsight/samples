import apiClient from "./apiClient";
import type { WeatherForecast } from "./types";

/**
 * Calls the protected GET /weatherforecast endpoint on ReactApp.Api.
 * The auth interceptor attaches a bearer token automatically.
 */
export async function getWeatherForecast(): Promise<WeatherForecast[]> {
  const response = await apiClient.get<WeatherForecast[]>("/weatherforecast");
  return response.data;
}
