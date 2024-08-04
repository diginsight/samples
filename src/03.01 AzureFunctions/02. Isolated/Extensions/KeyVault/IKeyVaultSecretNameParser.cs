using Azure.Security.KeyVault.Secrets;

namespace Isolatedsample;

public interface IKeyVaultSecretNameParser
{
    string Parse(KeyVaultSecret secret);
}
