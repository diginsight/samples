namespace AzureProvisioning.Configurations;

internal sealed class ArmClientCredentialsOptions : IArmClientCredentialsOptions
{
    public string? TenantId { get; set; }
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
}
