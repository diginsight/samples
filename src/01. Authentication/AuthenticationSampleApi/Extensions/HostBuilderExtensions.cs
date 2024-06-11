using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using System.Text;

namespace SampleWebApi;

public static class HostBuilderExtensions
{
    public static Type T = typeof(HostBuilderExtensions);
    public static IHostBuilder ConfigureAppConfiguration2(this IHostBuilder hostBuilder, Func<IDictionary<string, string>, bool>? tagsMatch = null)
    {
        var logger = Program.DeferredLoggerFactory.CreateLogger(T);
        using var activity = DiginsightDefaults.ActivitySource.StartMethodActivity(logger, () => new { hostBuilder, tagsMatch });

        return hostBuilder.ConfigureAppConfiguration((hbc, cb) => ConfigureAppConfiguration2(hbc.HostingEnvironment, cb, tagsMatch));
    }

    public static void ConfigureAppConfiguration2(
        IHostEnvironment environment, IConfigurationBuilder builder, Func<IDictionary<string, string>, bool>? tagsMatch = null
    )
    {
        var logger = Program.DeferredLoggerFactory.CreateLogger(T);
        using var activity = DiginsightDefaults.ActivitySource.StartMethodActivity(logger, () => new { environment, builder, tagsMatch});

        bool isLocal = environment.IsDevelopment();

        int? GetSourceIndex(Func<(IConfigurationSource Source, int Index), bool> predicate)
        {
            return builder.Sources
                .Select(static (source, index) => (Source: source, Index: index))
                .Where(predicate)
                .Select(static x => (int?)x.Index)
                .FirstOrDefault();
        }

        int GetJsonSourceIndex(string path)
        {
            return GetSourceIndex(x => x.Source is JsonConfigurationSource jsonSource && string.Equals(jsonSource.Path, path, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("No such json configuration source");
        }

        void AppendLocalJsonSource(string path, int index)
        {
            if (!isLocal) { return; }

            JsonConfigurationSource jsonSource = new JsonConfigurationSource()
            {
                Path = path,
                Optional = true,
                ReloadOnChange = true,
            };
            builder.Sources.Insert(index + 1, jsonSource);
        }

        int appsettingsIndex = GetJsonSourceIndex("appsettings.json");
        AppendLocalJsonSource("appsettings.local.json", appsettingsIndex);

        int appsettingsEnvIndex = GetJsonSourceIndex($"appsettings.{environment.EnvironmentName}.json");
        string? appsettingsEnvName = Environment.GetEnvironmentVariable("AppsettingsEnvironmentName");
        if (!string.IsNullOrEmpty(appsettingsEnvName))
        {
            ((JsonConfigurationSource)builder.Sources[appsettingsEnvIndex]).Path = $"appsettings.{appsettingsEnvName}.json";
        }

        AppendLocalJsonSource($"appsettings.{appsettingsEnvName ?? environment.EnvironmentName}.local.json", appsettingsEnvIndex);

        IConfiguration configuration = builder.Build();
        if (configuration["AzureKeyVault:Uri"] is { } kvUri && !string.IsNullOrEmpty(kvUri))
        {
            TokenCredential credential;
            if (isLocal)
            {
                var clientId = configuration["AzureKeyVault:ClientId"];
                var tenantId = configuration["AzureKeyVault:TenantId"];
                var clientSecret = configuration["AzureKeyVault:ClientSecret"];
                credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            }
            else
            {
                credential = new ManagedIdentityCredential();
            }

            builder.AddAzureKeyVault(new Uri(kvUri), credential, new KeyVaultSecretManager2(DateTimeOffset.UtcNow, tagsMatch));
        }

        int environmentVariablesIndex = GetSourceIndex(static x => x.Source is EnvironmentVariablesConfigurationSource) ?? -1;
        if (environmentVariablesIndex >= 0)
        {
            int sourcesCount = builder.Sources.Count;
            IConfigurationSource kvConfigurationSource = builder.Sources.Last();
            builder.Sources.RemoveAt(sourcesCount - 1);
            builder.Sources.Insert(environmentVariablesIndex, kvConfigurationSource);
        }
    }

    private sealed class KeyVaultSecretManager2 : KeyVaultSecretManager
    {
        private readonly DateTimeOffset now;
        private readonly Func<IDictionary<string, string>, bool>? tagsMatch;

        public KeyVaultSecretManager2(DateTimeOffset now, Func<IDictionary<string, string>, bool>? tagsMatch)
        {
            this.now = now;
            this.tagsMatch = tagsMatch;
        }

        public override string GetKey(KeyVaultSecret secret)
        {
            StringBuilder sb = new();
            ReadOnlySpan<char> name = secret.Name;

            bool lastWasDash = false;
            while (!name.IsEmpty)
            {
                char input = name[0];
                name = name[1..];

                char? maybeOutput = input switch
                {
                    '-' => ParseDash(ref name),
                    (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') => input,
                    _ => throw new ArgumentException("Unexpected character in secret name"),
                };

                if (maybeOutput is not { } output)
                    continue;

                if (output == ':')
                {
                    if (lastWasDash)
                    {
                        return sb.ToString()[..^1];
                    }
                    else
                    {
                        lastWasDash = true;
                    }
                }
                else
                {
                    lastWasDash = false;
                }

                sb.Append(output);
            }

            string key = sb.ToString();
            return lastWasDash ? key[..^1] : key;

            static char? ParseDash(ref ReadOnlySpan<char> name)
            {
                if (name.IsEmpty)
                    return null;

                char c = name[0];
                name = name[1..];
                return c switch
                {
                    '-' => ':',
                    'x' or 'X' => ParseHex(ref name, 2),
                    'u' or 'U' => ParseHex(ref name, 4),
                    _ => null,
                };
            }

            static char? ParseHex(ref ReadOnlySpan<char> name, int length)
            {
                if (name.Length < length)
                {
                    name = default;
                    return null;
                }

                string str = new string(name[..length]);
                name = name[length..];

                try
                {
                    return (char)Convert.ToUInt16(str, 16);
                }
                catch (FormatException)
                {
                    return null;
                }
            }
        }

        public override bool Load(SecretProperties secret)
        {
            var logger = Program.DeferredLoggerFactory.CreateLogger(T);
            using var activity = DiginsightDefaults.ActivitySource.StartMethodActivity(logger, new { secret });

            return secret.Enabled != false
                && !(secret.NotBefore > now)
                && !(secret.ExpiresOn < now)
                && (tagsMatch?.Invoke(secret.Tags) ?? true);
        }
    }
}

public static class WebHostBuilderExtensions
{
    public static Type T = typeof(WebHostBuilderExtensions);
    public static IWebHostBuilder ConfigureAppConfiguration2(this IWebHostBuilder hostBuilder, Func<IDictionary<string, string>, bool>? tagsMatch = null)
    {
        var logger = Program.DeferredLoggerFactory.CreateLogger(T);
        using var activity = DiginsightDefaults.ActivitySource.StartMethodActivity(logger);

        return hostBuilder.ConfigureAppConfiguration((whbc, cb) => HostBuilderExtensions.ConfigureAppConfiguration2(whbc.HostingEnvironment, cb, tagsMatch));
    }
}


