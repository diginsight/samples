using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace Isolatedsample;

public class DefaultKeyVaultSecretNameParser : IKeyVaultSecretNameParser
{
    public static readonly IKeyVaultSecretNameParser Default = new DefaultKeyVaultSecretNameParser();

    protected readonly string? tagKey;

    public DefaultKeyVaultSecretNameParser(string? tagKey = null)
    {
        this.tagKey = tagKey;
    }

    public virtual string Parse(KeyVaultSecret secret)
    {
        return tagKey is not null && secret.Properties.Tags.TryGetValue(tagKey, out string? tagValue)
            ? tagValue : secret.Name.Replace("--", ConfigurationPath.KeyDelimiter);
    }
}
