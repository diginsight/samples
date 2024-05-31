using Diginsight.Diagnostics;
using System.Diagnostics;

namespace SampleWebApi;

internal sealed class ActivitySourceDetector : IActivityListenerLogic
{
    private readonly ILogger logger;
    private readonly ISet<string> seenActivitySources = new HashSet<string>();

    public ActivitySourceDetector(ILogger<ActivitySourceDetector> logger)
    {
        this.logger = logger;
    }

    public void ActivityStarted(Activity activity)
    {
        string activitySourceName = activity.Source.Name;

        bool isNewActivitySource = false;
        lock (seenActivitySources) { isNewActivitySource = seenActivitySources.Add(activitySourceName); }

        if (isNewActivitySource)
        {
            logger.LogDebug("New activity source detected: {ActivitySource}", activitySourceName);
        }
    }
}
