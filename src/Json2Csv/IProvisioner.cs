using Diginsight.Diagnostics;
using Microsoft.Extensions.Logging;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Json2Csv;

internal interface IProvisioner
{
    Task Convert(CancellationToken cancellationToken);
}

internal sealed class Provisioner : IProvisioner
{
    private readonly ILogger logger;

    public Provisioner(ILogger<Provisioner> logger)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        this.logger = logger;
    }

    public async Task Convert(CancellationToken cancellationToken)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        logger.LogInformation("Groups creation START - groups: {groups}");
        try
        {
            //foreach ((string groupName, IEnumerable<string> owners) in ownersByGroup)
            //{
            //    cancellationToken.ThrowIfCancellationRequested();
            //    await EnsureGroupAsync(groupName, owners);
            //}
        }
        catch (Exception exception) { logger.LogError(exception, "Exception {type} ensuring groups: {exceptionMessage}", exception.GetType().Name, exception.Message); }
        finally { logger.LogInformation("Groups creation END"); }
    }

}
