
using Diginsight.CAOptions;

namespace AuthenticationSampleApi;

public sealed class OpenTelemetryOptions
{
    public bool EnableTraces { get; set; }
    public bool EnableMetrics { get; set; }

    public double TracingSamplingRatio { get; set; }

    public ICollection<string> ActivitySources { get; } = new List<string>();
    public ICollection<string> Meters { get; } = new List<string>();

    public ICollection<string> ExcludedHttpHosts { get; } = new List<string>();

    public ICollection<string> DurationMetricTags { get; } = new List<string>();
}
