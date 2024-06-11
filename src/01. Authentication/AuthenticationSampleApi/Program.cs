using Diginsight.Diagnostics;
using Diginsight.Diagnostics.AspNetCore;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
namespace SampleWebApi;

public class Program
{
    public static IDeferredLoggerFactory DeferredLoggerFactory;
    internal static readonly ActivitySource ActivitySource = new(typeof(Program).Namespace ?? typeof(Program).Name!);

    public static void Main(string[] args)
    {
        DiginsightActivitiesOptions activitiesOptions = new() { LogActivities = true };
        DeferredLoggerFactory = new DeferredLoggerFactory(activitiesOptions: activitiesOptions);
        var logger = DeferredLoggerFactory.CreateLogger<Program>();

        ActivitySource activitySource = new(typeof(Program).Namespace!);
        DeferredLoggerFactory.ActivitySources.Add(activitySource);
        DiginsightDefaults.ActivitySource = activitySource;

        IWebHost host;
        using (var activity = DiginsightDefaults.ActivitySource.StartMethodActivity(logger, new { args }))
        {
            host = WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration2()
                .UseStartup<Startup>()
                .ConfigureServices(services =>
                {
                    var logger = DeferredLoggerFactory.CreateLogger<Startup>();
                    using var innerActivity = ActivitySource.StartRichActivity(logger, "ConfigureServicesCallback", new { services });

                    services.TryAddSingleton(DeferredLoggerFactory);
                })
                .UseDiginsightServiceProvider()
                .Build();

            logger.LogDebug("Host built");
        }

        host.Run();
    }
}
