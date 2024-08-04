#nullable enable
using Azure.Core;
using Azure.Identity;
using Diginsight.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Isolatedsample;

public sealed class MyKeyVaultCredentialProvider : IKeyVaultCredentialProvider
{
    public (Uri Uri, TokenCredential Credential)? Get(IConfiguration configuration, IHostEnvironment environment)
    {
        //ILogger logger = Program.StartupLoggerFactory.CreateLogger<MyKeyVaultCredentialProvider>();
        //using Activity? activity = Observability.ActivitySource.StartMethodActivity(logger);

        const string prefix = "AzureKeyVault:";

        string? kvUri = configuration[$"{prefix}Uri"];
        if (string.IsNullOrEmpty(kvUri)) { return null; }

        bool isLocal = environment.IsDevelopment();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static string? HardTrim(string? s) => s?.Trim() is not "" and var s0 ? s0 : null;

        string? tenantId = HardTrim(configuration[$"{prefix}TenantId"]);
        //logger.LogDebug("KV tenant id: {TenantId}", tenantId);

        string? clientId = HardTrim(configuration[$"{prefix}ClientId"]);
        //logger.LogDebug("KV client id: {ClientId}", clientId);

        string? clientSecret = HardTrim(configuration[$"{prefix}ClientSecret"]);
        //logger.LogDebug("KV client secret (hint): {ClientSecret}", clientSecret?[..3]);

        string appsettingsEnvName = Environment.GetEnvironmentVariable("AppsettingsEnvironmentName") ?? environment.EnvironmentName;
        //logger.LogDebug("Appsettings environment name: {AppsettingsEnvironmentName}", appsettingsEnvName);
        bool isChina = appsettingsEnvName.EndsWith("cn", StringComparison.OrdinalIgnoreCase);

        TokenCredential credential;
        if (tenantId is not null && clientId is not null && clientSecret is not null)
        {
            ClientSecretCredentialOptions credentialOptions = new ();
            if (isChina)
            {
                credentialOptions.AuthorityHost = AzureAuthorityHosts.AzureChina;
            }

            credential = new ClientSecretCredential(tenantId, clientId, clientSecret, credentialOptions);
        }
        else if (isLocal)
        {
            AzureCliCredentialOptions credentialOptions = new ();
            if (isChina)
            {
                credentialOptions.AuthorityHost = AzureAuthorityHosts.AzureChina;
            }

            credential = new ChainedTokenCredential(new AzureCliCredential(credentialOptions));
        }
        else
        {
            throw new ArgumentException("Tenant id, client id or client secret is empty");
        }

        return (new Uri(kvUri), credential);
    }
}
