using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Isolatedsample;

public sealed class DefaultKeyVaultCredentialProvider : IKeyVaultCredentialProvider
{
    public const string DefaultKvSectionName = "AzureKeyVault";

    public static readonly IKeyVaultCredentialProvider Default = new DefaultKeyVaultCredentialProvider();

    private readonly string kvSectionName;

    public DefaultKeyVaultCredentialProvider(string? kvSectionName = null)
    {
        this.kvSectionName = kvSectionName ?? DefaultKvSectionName;
    }

    public (Uri Uri, TokenCredential Credential)? Get(IConfiguration configuration, IHostEnvironment environment)
    {
        string? kvUri = configuration[$"{kvSectionName}:Uri"];
        if (string.IsNullOrEmpty(kvUri))
        {
            return null;
        }

        TokenCredential credential;
        if (environment.IsDevelopment())
        {
            string GetOrThrow(string key)
            {
                string? value = configuration[key];
                return string.IsNullOrEmpty(value)
                    ? throw new ArgumentNullException($"Configuration section {key} is empty", (Exception?)null)
                    : value;
            }

            string clientId = GetOrThrow($"{kvSectionName}:ClientId");
            string tenantId = GetOrThrow($"{kvSectionName}:TenantId");
            string clientSecret = GetOrThrow($"{kvSectionName}:ClientSecret");

            ClientSecretCredentialOptions credentialOptions = new ();
            string appsettingsEnvName = Environment.GetEnvironmentVariable("AppsettingsEnvironmentName") ?? environment.EnvironmentName;
            if (appsettingsEnvName.EndsWith("cn", StringComparison.OrdinalIgnoreCase))
            {
                credentialOptions.AuthorityHost = AzureAuthorityHosts.AzureChina;
            }

            credential = new ClientSecretCredential(tenantId, clientId, clientSecret, credentialOptions);
        }
        else
        {
            credential = new ManagedIdentityCredential();
        }

        return (new Uri(kvUri), credential);
    }
}
