namespace BlazorApp.Client.Configuration
{
    /// <summary>
    /// Tracks whether the client successfully retrieved its authentication configuration
    /// from the server (<c>BlazorApp.Api</c>) at startup. UI components can use this to keep
    /// the app usable even when the server / auth configuration is not available yet.
    /// </summary>
    public sealed class ClientAuthBootstrapState
    {
        public bool IsConfigLoaded { get; init; }

        public bool IsAuthenticationConfigured { get; init; }
    }
}
