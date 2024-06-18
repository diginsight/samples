using Diginsight.Diagnostics;
using Diginsight.Diagnostics.AspNetCore;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics;
namespace AuthenticationSampleApi;

public class Program
{
    public static IDeferredLoggerFactory DeferredLoggerFactory;

    public static void Main(string[] args)
    {
        var activitiesOptions = new DiginsightActivitiesOptions() { LogActivities = true };
        DeferredLoggerFactory = new DeferredLoggerFactory(activitiesOptions: activitiesOptions);
        DeferredLoggerFactory.ActivitySources.Add(Observability.ActivitySource);
        var logger = DeferredLoggerFactory.CreateLogger<Program>();

        IWebHost host;
        using (var activity = Observability.ActivitySource.StartMethodActivity(logger, new { args }))
        {
            host = WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration2()
                .UseStartup<Startup>()
                .ConfigureServices(services =>
                {
                    var logger = DeferredLoggerFactory.CreateLogger<Startup>();
                    using var innerActivity = Observability.ActivitySource.StartRichActivity(logger, "ConfigureServicesCallback", new { services });

                    services.TryAddSingleton(DeferredLoggerFactory);
                })
                .UseDiginsightServiceProvider()
                .Build();

            logger.LogDebug("Host built");
        }

        host.Run();
    }
}