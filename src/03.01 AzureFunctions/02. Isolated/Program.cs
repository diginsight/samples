using Diginsight;
using Diginsight.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Isolatedsample;

internal static class Program
{
    private static void Main()
    {
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
        AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromMilliseconds(100));

        DiginsightActivitiesOptions activitiesOptions = new () { LogActivities = true };
        IDeferredLoggerFactory deferredLoggerFactory = new DeferredLoggerFactory(activitiesOptions: activitiesOptions);
        deferredLoggerFactory.ActivitySources.Add(Observability.ActivitySource);
        ILogger logger = deferredLoggerFactory.CreateLogger(typeof(Program));

        IHost host;
        using (Observability.ActivitySource.StartMethodActivity(logger))
        {
            host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .ConfigureAppConfiguration(
                    static (hbc, configurationBuilder) =>
                    {
                        IHostEnvironment hostEnvironment = hbc.HostingEnvironment;
                        var hostEnvironmentName = Environment.GetEnvironmentVariable(hostEnvironment.EnvironmentName);
                        var applicationEnvironment = Environment.GetEnvironmentVariable("AppsettingsEnvironmentName");
                        Console.WriteLine($"hostEnvironmentName: {hostEnvironmentName}");
                        Console.WriteLine($"applicationEnvironment: {applicationEnvironment}");

                        int currentIndex = 1;
                        configurationBuilder.Sources.Insert(
                            currentIndex++,
                            new JsonConfigurationSource() { Path = "appsettings.json", Optional = true, ReloadOnChange = true }
                        );
                        configurationBuilder.Sources.Insert(
                            currentIndex++,
                            new JsonConfigurationSource() { Path = $"appsettings.{applicationEnvironment}.json", Optional = true, ReloadOnChange = true }
                        );

                        if (hostEnvironment.IsDevelopment())
                        {
                            bool hasUserSecrets;
                            try
                            {
                                configurationBuilder.AddUserSecrets(typeof(Program).Assembly, optional: false);
                                hasUserSecrets = true;
                            }
                            catch (InvalidOperationException )
                            {
                                hasUserSecrets = false;
                            }

                            if (hasUserSecrets)
                            {
                                int lastIndex = configurationBuilder.Sources.Count - 1;
                                IConfigurationSource userSecretsSource = configurationBuilder.Sources[lastIndex];
                                configurationBuilder.Sources.RemoveAt(lastIndex);
                                configurationBuilder.Sources.Insert(currentIndex, userSecretsSource);
                            }
                        }
                    }
                )
                .ConfigureAppConfigurationNH(kvCredentialProvider: new MyKeyVaultCredentialProvider())
                .ConfigureServices((hbc, services) => ConfigureServices(services, hbc.Configuration, hbc.HostingEnvironment, deferredLoggerFactory, logger))
                .UseDiginsightServiceProvider()
                .Build();
        }

        host.Run();
    }

    private static void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        IDeferredLoggerFactory deferredLoggerFactory,
        ILogger logger
    )
    {
        using Activity? activity = Observability.ActivitySource.StartMethodActivity(logger);

        //services.AddAspNetCoreObservability(configuration, hostEnvironment);

        services.FlushOnCreateServiceProvider(deferredLoggerFactory);

        services.AddHttpClient(Options.DefaultName)
            .ConfigureHttpClient(
                static (sp, hc) =>
                {
                    GeneralOptions bomOptions = sp.GetRequiredService<IOptions<GeneralOptions>>().Value;

                    hc.Timeout = TimeSpan.FromMinutes(3);
                    hc.BaseAddress = new Uri(bomOptions.BaseUrl);
                }
            );

        services.Configure<GeneralOptions>(configuration.GetSection("Bom"));
        services.TryAddSingleton<TimerFunctions>();
    }
}
