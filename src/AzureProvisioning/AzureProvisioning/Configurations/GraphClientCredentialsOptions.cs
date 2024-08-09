namespace AzureProvisioning.Configurations;

internal sealed class GraphClientCredentialsOptions : IGraphClientCredentialsOptions
{
    public string? TenantId { get; set; }
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
}
