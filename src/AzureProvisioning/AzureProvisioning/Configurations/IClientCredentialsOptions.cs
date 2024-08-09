namespace AzureProvisioning.Configurations;

internal interface IClientCredentialsOptions
{
    string? TenantId { get; }
    string ClientId { get; }
    string ClientSecret { get; }
}
