using Diginsight.CAOptions;

namespace SampleWebApi;

public class FeatureFlagOptions : IDynamicallyPostConfigurable
{
    public bool TraceRequestBody { get; set; }
    public bool TraceResponseBody { get; set; }

}
