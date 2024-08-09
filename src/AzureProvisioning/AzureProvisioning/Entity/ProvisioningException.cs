namespace AzureProvisioning;

internal class ProvisioningException : ApplicationException
{
    public ProvisioningException(string? message)
        : base(message) { }

    public ProvisioningException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
