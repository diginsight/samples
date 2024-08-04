using Azure.Security.KeyVault.Secrets;

namespace Isolatedsample;

public class PlainKeyVaultSecretNameParser : IKeyVaultSecretNameParser
{
    public static readonly IKeyVaultSecretNameParser Default = new PlainKeyVaultSecretNameParser();

    protected readonly string? tagKey;

    public PlainKeyVaultSecretNameParser(string? tagKey = null)
    {
        this.tagKey = tagKey;
    }

    public virtual string Parse(KeyVaultSecret secret)
    {
        return tagKey is not null && secret.Properties.Tags.TryGetValue(tagKey, out string? tagValue)
            ? tagValue : secret.Name;
    }
}
