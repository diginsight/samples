using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace BlazorApp.Client.Services
{
    /// <summary>
    /// Options describing how to reach the protected API (BlazorApp.Api): its base URL and the
    /// access token scopes to request. Values are provided by the server at startup so the client
    /// does not hardcode the API app registration.
    /// </summary>
    public sealed class ApiClientOptions
    {
        public string BaseUrl { get; init; } = string.Empty;

        public string[] Scopes { get; init; } = [];
    }

    /// <summary>
    /// Attaches an access token (acquired for the API scopes) to outgoing requests to the API.
    /// </summary>
    public sealed class ApiAuthorizationMessageHandler : AuthorizationMessageHandler
    {
        public ApiAuthorizationMessageHandler(
            IAccessTokenProvider provider,
            NavigationManager navigation,
            ApiClientOptions options)
            : base(provider, navigation)
        {
            ConfigureHandler(
                authorizedUrls: [options.BaseUrl],
                scopes: options.Scopes);
        }
    }
}
