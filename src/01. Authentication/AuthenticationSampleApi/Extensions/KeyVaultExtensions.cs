using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace SampleWebApi;

public static class KeyVaultExtensions
{
    public static IHostBuilder AddKeyVault(this IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureAppConfiguration(static (context, builder) => { ConfigureKeyVault(context.HostingEnvironment, builder); });
        return hostBuilder;
    }

    public static IWebHostBuilder AddKeyVault(this IWebHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureAppConfiguration(static (context, builder) => { ConfigureKeyVault(context.HostingEnvironment, builder); });
        return hostBuilder;
    }

    private static void ConfigureKeyVault(IHostEnvironment env, IConfigurationBuilder builder)
    {
        bool isLocal = env.IsDevelopment();
        int appsettingsEnvIndex = builder.Sources
            .Select(static (source, index) => (source, index))
            .Where(x => x.source is JsonConfigurationSource jsonSource && jsonSource.Path == $"appsettings.{env.EnvironmentName}.json")
            .Select(static x => x.index)
            .First();

        string appsettingsEnvName = Environment.GetEnvironmentVariable("AppsettingsEnvironmentName");
        if (!string.IsNullOrEmpty(appsettingsEnvName))
        {
            ((JsonConfigurationSource)builder.Sources[appsettingsEnvIndex]).Path = $"appsettings.{appsettingsEnvName}.json";
        }

        if (isLocal)
        {
            JsonConfigurationSource appsettingsEnvLocalSource = new JsonConfigurationSource()
            {
                Path = $"appsettings.{appsettingsEnvName ?? env.EnvironmentName}.local.json",
                Optional = true,
                ReloadOnChange = true,
            };
            builder.Sources.Insert(appsettingsEnvIndex + 1, appsettingsEnvLocalSource);
        }

        IConfiguration configuration = builder.Build();
        TokenCredential credential = null;
        if (isLocal)
        {
            var clientId = configuration["AzureKeyVault:ClientId"];
            var tenantId = configuration["AzureKeyVault:TenantId"];
            var clientSecret = configuration["AzureKeyVault:ClientSecret"];

            if (!string.IsNullOrEmpty(clientId))
            {
                var credentialOptions = new ClientSecretCredentialOptions();
                if (appsettingsEnvName?.EndsWith("cn", StringComparison.OrdinalIgnoreCase) ?? false) { credentialOptions.AuthorityHost = AzureAuthorityHosts.AzureChina; }

                credential = new ClientSecretCredential(tenantId, clientId, clientSecret, credentialOptions);
            }
        }
        else
        {
            credential = new ManagedIdentityCredential();
        }

        var akvUri = configuration["AzureKeyVault:Uri"];
        if (!string.IsNullOrEmpty(akvUri)) { builder.AddAzureKeyVault(new Uri(akvUri), credential); }

        int environmentVariablesIndex = builder.Sources
            .Select(static (source, index) => (source, index))
            .Where(static x => x.source is EnvironmentVariablesConfigurationSource)
            .Select(static x => x.index)
            .LastOrDefault(-1);
        if (environmentVariablesIndex >= 0)
        {
            int sourcesCount = builder.Sources.Count;
            var environmentVariablesSource = builder.Sources[environmentVariablesIndex];
            builder.Sources.RemoveAt(environmentVariablesIndex);
            builder.Sources.Add(environmentVariablesSource);
        }
    }
}
