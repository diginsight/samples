using Diginsight.Diagnostics;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace AuthenticationSampleApi;

internal sealed class ActivitySourceDetector : IActivityListenerLogic
{
    private readonly ILogger logger;
    private readonly ConcurrentDictionary<string, ValueTuple> seenActivitySources = new ConcurrentDictionary<string, ValueTuple>();

    public ActivitySourceDetector(ILogger<ActivitySourceDetector> logger)
    {
        this.logger = logger;
    }

    public void ActivityStarted(Activity activity)
    {
        string activitySourceName = activity.Source.Name;

        if (seenActivitySources.TryAdd(activitySourceName, default))
        {
            logger.LogDebug("New activity source detected: {ActivitySource}", activitySourceName);
        }
    }
}

