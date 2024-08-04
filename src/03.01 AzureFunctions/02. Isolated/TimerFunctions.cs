using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace Isolatedsample;

public sealed class TimerFunctions
{
    private readonly ILogger logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly GeneralOptions generalOptions;

    public TimerFunctions(
        ILogger<TimerFunctions> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<GeneralOptions> bomOptions
    )
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.generalOptions = bomOptions.Value;
    }

    [Function("TriggerNotifications")]
    public async Task TriggerNotificationsAsync([TimerTrigger("%NotificationsTimer%", RunOnStartup = true)] TimerInfo timerInfo)
    {
        _ = timerInfo;

        try
        {
            var httpClient = httpClientFactory.CreateClient();

            using var httpResponse = await httpClient.GetAsync($"{generalOptions.RelativeUrl}");
            var responseContent = await httpResponse.Content.ReadAsStringAsync();
            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                logger.LogDebug("Response: {Response}", responseContent);
            }
            else
            {
                logger.LogWarning("Response: {Response}", responseContent);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception has occurred while triggering the BOM notifications.");
        }
    }
}
