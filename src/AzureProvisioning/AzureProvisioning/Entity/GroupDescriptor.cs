namespace AzureProvisioning;

internal sealed record GroupDescriptor(IEnumerable<string> Members, IEnumerable<string> Owners);
