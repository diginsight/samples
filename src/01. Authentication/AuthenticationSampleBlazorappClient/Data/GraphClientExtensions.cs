using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Authentication.WebAssembly.Msal.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions;

/// <summary>
/// Adds services and implements methods to use Microsoft Graph SDK.
/// </summary>

internal static class GraphClientExtensions
{
    public static IServiceCollection AddGraphClient(
        this IServiceCollection services, string? baseUrl, List<string>? scopes)
    {
        services.Configure<RemoteAuthenticationOptions<MsalProviderOptions>>(
            options =>
            {
                scopes?.ForEach((scope) =>
                {
                    options.ProviderOptions.AdditionalScopesToConsent.Add(scope);
                });
            });
        services.AddScoped<IAuthenticationProvider, GraphAuthenticationProvider>();
        services.AddScoped(sp =>
        {
            return new GraphServiceClient(
                new HttpClient(),
                sp.GetRequiredService<IAuthenticationProvider>(),
                baseUrl);
        });
        return services;
    }

    private class GraphAuthenticationProvider : IAuthenticationProvider
    {
        private readonly IConfiguration config;

        public GraphAuthenticationProvider(Microsoft.AspNetCore.Components.WebAssembly.Authentication.IAccessTokenProvider tokenProvider,
            IConfiguration config)
        {
            TokenProvider = tokenProvider;
            this.config = config;
        }

        public Microsoft.AspNetCore.Components.WebAssembly.Authentication.IAccessTokenProvider TokenProvider { get; }

        public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            var result = await TokenProvider.RequestAccessToken(
                new AccessTokenRequestOptions()
                {
                    Scopes = config.GetSection("MicrosoftGraph:Scopes").Get<string[]>()
                });

            if (result.TryGetToken(out var token))
            {
                //request.Headers.Authorization ??= new AuthenticationHeaderValue("Bearer", token.Value);
                request.Headers.Add("Authorization", "Bearer " + token.Value);
            }
        }
    }
}