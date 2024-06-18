using Asp.Versioning;
using Diginsight.CAOptions;

namespace AuthenticationSampleApi;

public static class ApiVersions
{
    public static class V_2024_04_26
    {
        public const string Name = "2024-04-26";
        public static ApiVersion Version = new ApiVersion(new DateOnly(2024, 04, 26));
    }
}