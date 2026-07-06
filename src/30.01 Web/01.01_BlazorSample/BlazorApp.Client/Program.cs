using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Net.Http.Json;
using BlazorApp.Client.Configuration;
using BlazorApp.Client.Services;
using BlazorApp.Models;

namespace BlazorApp.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            // Retrieve the app registration (client id / authority) from the server (BlazorApp.Api)
            // so that no identity value needs to be stored in the client application.
            // The API that serves this client also exposes the config/API endpoints under the same
            // virtual path, so default the base URL to the app's own base address (which already
            // includes the configured "/{pathBase}"). An explicit ServerConfig:BaseUrl still overrides.
            var serverConfigBaseUrl = builder.Configuration["ServerConfig:BaseUrl"];
            if (string.IsNullOrWhiteSpace(serverConfigBaseUrl))
            {
                serverConfigBaseUrl = builder.HostEnvironment.BaseAddress;
            }
            var authEndpoint = builder.Configuration["ServerConfig:AuthEndpoint"] ?? "api/clientconfig/auth";
            var serverAuthConfig = await TryLoadServerAuthConfigAsync(serverConfigBaseUrl, authEndpoint);

            builder.Services.AddSingleton(new ClientAuthBootstrapState
            {
                IsConfigLoaded = serverAuthConfig is not null,
                IsAuthenticationConfigured = !string.IsNullOrWhiteSpace(serverAuthConfig?.ClientId),
            });

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            // Scopes required to call the protected API (BlazorApp.Api), advertised by the server.
            var apiScopes = (serverAuthConfig?.Scopes ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            builder.Services.AddMsalAuthentication(options =>
            {
                builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);

                if (!string.IsNullOrWhiteSpace(serverAuthConfig?.ClientId))
                {
                    options.ProviderOptions.Authentication.ClientId = serverAuthConfig.ClientId;
                }

                if (!string.IsNullOrWhiteSpace(serverAuthConfig?.Authority))
                {
                    options.ProviderOptions.Authentication.Authority = serverAuthConfig.Authority;
                }

                if (serverAuthConfig?.ValidateAuthority is not null)
                {
                    options.ProviderOptions.Authentication.ValidateAuthority = serverAuthConfig.ValidateAuthority.Value;
                }

                // Request (and consent to) the API scope at sign-in so a valid access token for the
                // API audience is available when the client calls it.
                foreach (var scope in apiScopes)
                {
                    options.ProviderOptions.DefaultAccessTokenScopes.Add(scope);
                }

                if (string.IsNullOrWhiteSpace(options.ProviderOptions.Authentication.ClientId))
                {
                    // Keep app startup non-blocking; auth-only features remain disabled in the UI
                    // until the server configuration becomes available.
                    options.ProviderOptions.Authentication.ClientId = "auth-not-configured";
                }
            });

            // Typed HttpClient that calls the protected API (BlazorApp.Api) with an access token
            // acquired for the scopes advertised by the server. Only registered when a scope is
            // available, otherwise a wrong-audience token would be sent and rejected by the API.
            if (!string.IsNullOrWhiteSpace(serverConfigBaseUrl) && apiScopes.Length > 0)
            {
                builder.Services.AddSingleton(new ApiClientOptions
                {
                    BaseUrl = serverConfigBaseUrl,
                    Scopes = apiScopes,
                });
                builder.Services.AddScoped<ApiAuthorizationMessageHandler>();
                builder.Services.AddHttpClient("BlazorApp.Api", client => client.BaseAddress = new Uri(serverConfigBaseUrl))
                    .AddHttpMessageHandler<ApiAuthorizationMessageHandler>();
            }

            await builder.Build().RunAsync();
        }

        private static async Task<ClientAuthConfigResponse?> TryLoadServerAuthConfigAsync(string? serverConfigBaseUrl, string authEndpoint)
        {
            if (string.IsNullOrWhiteSpace(serverConfigBaseUrl))
            {
                return null;
            }

            using var configurationHttpClient = new HttpClient { BaseAddress = new Uri(serverConfigBaseUrl) };

            // The API can take a while to start (Key Vault + OpenTelemetry init), so retry a few times
            // instead of failing fast. Only accept a response that actually carries a client id.
            for (var attempt = 1; attempt <= 6; attempt++)
            {
                try
                {
                    using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var config = await configurationHttpClient.GetFromJsonAsync<ClientAuthConfigResponse>(authEndpoint, cancellationTokenSource.Token);
                    if (!string.IsNullOrWhiteSpace(config?.ClientId))
                    {
                        return config;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    // API not reachable yet; fall through and retry.
                }

                if (attempt < 6)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }

            return null;
        }
    }
}
