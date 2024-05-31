using OpenTelemetry;
using System.Diagnostics;
using Diginsight.Diagnostics;

namespace SampleWebApi;

internal sealed class ActivitySourceDetectorRegistration : IActivityListenerRegistration
{
    public IActivityListenerLogic Logic { get; }

    public ActivitySourceDetectorRegistration(IServiceProvider serviceProvider)
    {
        Logic = ActivatorUtilities.CreateInstance<ActivitySourceDetector>(serviceProvider);
    }

    public bool ShouldListenTo(ActivitySource activitySource) => true;
}
