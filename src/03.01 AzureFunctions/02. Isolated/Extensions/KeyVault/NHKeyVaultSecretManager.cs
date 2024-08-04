using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace Isolatedsample;

public sealed class NHKeyVaultSecretManager : KeyVaultSecretManager
{
    private readonly Func<IDictionary<string, string>, bool>? tagsMatch;
    private readonly IKeyVaultSecretNameParser nameParser;
    private readonly TimeProvider timeProvider;

    public NHKeyVaultSecretManager(
        Func<IDictionary<string, string>, bool>? tagsMatch = null,
        IKeyVaultSecretNameParser? nameParser = null,
        TimeProvider? timeProvider = null
    )
    {
        this.tagsMatch = tagsMatch;
        this.nameParser = nameParser ?? DefaultKeyVaultSecretNameParser.Default;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public override string GetKey(KeyVaultSecret secret) => nameParser.Parse(secret);

    public override bool Load(SecretProperties secret)
    {
        if (secret.Enabled != true)
        {
            return false;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        return !(secret.NotBefore > now)
            && !(secret.ExpiresOn < now)
            && (tagsMatch?.Invoke(secret.Tags) ?? true);
    }
}
