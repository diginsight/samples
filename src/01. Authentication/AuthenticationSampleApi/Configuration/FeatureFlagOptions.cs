using Diginsight.CAOptions;

namespace AuthenticationSampleApi;

public class FeatureFlagOptions : IVolatilelyConfigurable, IDynamicallyConfigurable
{
    public bool TraceRequestBody { get; set; }
    public bool TraceResponseBody { get; set; }

}
