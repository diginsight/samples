using Cocona;
using Cocona.Builder;
using Page2Clipboard;
using Diginsight.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Diginsight;

namespace Page2Clipboard
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            using var observabilityManager = new ObservabilityManager();
            ILogger logger = observabilityManager.LoggerFactory.CreateLogger(typeof(Program));
            //ObservabilityRegistry.RegisterLoggerFactory(observabilityManager.LoggerFactory);

            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

            CoconaApp app = default!;
            using (var activity = Observability.ActivitySource.StartMethodActivity(logger, new { args }))
            {
                CoconaAppBuilder appBuilder = CoconaApp.CreateBuilder(args);

                IConfiguration configuration = appBuilder.Configuration;
                IServiceCollection services = appBuilder.Services;
                IHostEnvironment hostEnvironment = appBuilder.Environment;

                services.AddObservability(configuration, hostEnvironment);
                observabilityManager.AttachTo(services);
                services.TryAddSingleton<IActivityLoggingFilter, OptionsBasedActivityLoggingFilter>();

                services.AddSingleton<Executor>();

                appBuilder.Host.UseDiginsightServiceProvider(true);
                app = appBuilder.Build();


                Executor executor = app.Services.GetRequiredService<Executor>();
                app.AddCommand("url", executor.InvokeAsync);
            }

            await app.RunAsync();
        }
    }
}
