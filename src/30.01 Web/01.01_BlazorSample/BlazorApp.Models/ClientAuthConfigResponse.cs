namespace BlazorApp.Models
{
    /// <summary>
    /// Authentication configuration served by the API (<c>BlazorApp.Api</c>) to the
    /// Blazor WebAssembly client (<c>BlazorApp.Client</c>).
    /// This allows the client to obtain its app registration (client id / authority)
    /// from the server at startup, so no identity value needs to be stored in the client.
    /// </summary>
    public sealed class ClientAuthConfigResponse
    {
        public string? ClientId { get; set; }

        public string? Authority { get; set; }

        public bool? ValidateAuthority { get; set; }

        /// <summary>
        /// Space-separated access token scopes the client must request to call the protected API
        /// (e.g. <c>api://&lt;api-app-id&gt;/access_as_user</c>). Provided by the server so the client
        /// does not need to hardcode the API app registration.
        /// </summary>
        public string? Scopes { get; set; }
    }
}
