
using Diginsight.CAOptions;

namespace SampleWebApi;

public sealed class OpenTelemetryOptions : IDynamicallyPostConfigurable
{
    public bool EnableTraces { get; set; }
    public bool EnableMetrics { get; set; }
    public double TracingSamplingRatio { get; set; }
    public ICollection<string> ActivitySources { get; } = new List<string>();
    public ICollection<string> ExcludedHttpHosts { get; } = new List<string>();
    public ICollection<string> DurationMetricTags { get; } = new List<string>();

    object IDynamicallyPostConfigurable.MakeFiller() => new Filler(this);
    private sealed class Filler
    {
        private readonly OpenTelemetryOptions filled;

        public bool EnableTraces
        {
            get => filled.EnableTraces;
            set => filled.EnableTraces = value;
        }

        public bool EnableMetrics
        {
            get => filled.EnableMetrics;
            set => filled.EnableMetrics = value;
        }

        public double TracingSamplingRatio
        {
            get => filled.TracingSamplingRatio;
            set => filled.TracingSamplingRatio = value;
        }

        public Filler(OpenTelemetryOptions filled)
        {
            this.filled = filled;
        }
    }
}

