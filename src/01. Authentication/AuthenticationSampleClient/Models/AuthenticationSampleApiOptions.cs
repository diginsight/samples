using Diginsight.Options;

namespace AuthenticationSampleClient;

public class AuthenticationSampleApiOptions : IDynamicallyConfigurable
{
    public required string TenantId { get; set; }
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string Uri { get; set; }
}
