using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Isolatedsample;

public interface IKeyVaultCredentialProvider
{
    (Uri Uri, TokenCredential Credential)? Get(IConfiguration configuration, IHostEnvironment environment);
}
