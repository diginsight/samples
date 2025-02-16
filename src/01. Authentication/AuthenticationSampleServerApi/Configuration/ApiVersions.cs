using Asp.Versioning;
using Diginsight.Options;

namespace AuthenticationSampleServerApi;

/// <summary>
/// Contains API version information.
/// </summary>
public static class ApiVersions
{
    /// <summary>
    /// API version 2024-04-26.
    /// </summary>
    public static class V_2024_04_26
    {
        /// <summary>
        /// The name of the API version.
        /// </summary>
        public const string Name = "2024-04-26";

        /// <summary>
        /// The API version.
        /// </summary>
        public static readonly ApiVersion Version = new ApiVersion(new DateOnly(2024, 04, 26));
    }
}
