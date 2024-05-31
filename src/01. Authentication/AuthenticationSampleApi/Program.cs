using Diginsight.Diagnostics.AspNetCore;
using Microsoft.AspNetCore;
using System.Diagnostics;
namespace SampleWebApi;

public class Program
{
    internal static readonly ActivitySource ActivitySource = new(typeof(Program).Namespace ?? typeof(Program).Name!);

    public static void Main(string[] args)
    {
        //DiginsightActivitiesOptions activitiesOptions = new ()
        //{
        //    LogActivities = true,
        //};
        //IDeferredLoggerFactory loggerFactory = new DeferredLoggerFactory(activitiesOptions: activitiesOptions);
        //ILogger logger = loggerFactory.CreateLogger<Program>();
        //ActivitySource deferredActivitySource = loggerFactory.ActivitySource;

        IWebHost host;
        //using (deferredActivitySource.StartMethodActivity(logger))
        {
            host = WebHost.CreateDefaultBuilder(args)
                .AddKeyVault()
                .UseStartup<Startup>()
                //.ConfigureServices(s => s.FlushOnCreateServiceProvider(loggerFactory))
                .UseDiginsightServiceProvider()
                .Build();

            //logger.LogDebug("Host built");
        }

        host.Run();
    }
}