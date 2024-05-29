#region using
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using static System.Formats.Asn1.AsnWriter;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Refit;
using Polly;
using Diginsight;
using Diginsight.Diagnostics;
using Diginsight.Diagnostics.Log4Net;
using log4net.Appender;
using System.IO;
using log4net.Repository.Hierarchy;
#endregion

namespace AuthenticationSample
{
    /// <summary>Interaction logic for App.xaml</summary>
    public partial class App : Application
    {
        const string CONFIGVALUE_APPINSIGHTSKEY = "AppInsightsKey", DEFAULTVALUE_APPINSIGHTSKEY = "";
        internal static readonly DeferredLoggerFactory DeferredLoggerFactory;
        internal static readonly ActivitySource DeferredActivitySource = new(typeof(App).Namespace ?? typeof(App).Name!);
        internal static readonly ActivitySource ActivitySource = new(typeof(App).Namespace ?? typeof(App).Name!);
        static Type T = typeof(App);
        public static IHost Host;
        private ILogger<App> logger;

        static App()
        {
            DiginsightActivitiesOptions activitiesOptions = new() { LogActivities = true };
            DeferredLoggerFactory = new DeferredLoggerFactory(activitiesOptions: activitiesOptions);
            var logger = DeferredLoggerFactory.CreateLogger<App>();
            DeferredActivitySource = DeferredLoggerFactory.ActivitySource;

            using var activity = DeferredActivitySource.StartMethodActivity(logger);

            try
            {
                // logger.LogDebug("this is a debug trace");
                // logger.LogInformation("this is a Information trace");
                // logger.LogWarning("this is a Warning trace");
                // logger.LogError("this is a error trace");
                throw new InvalidOperationException("this is an exception");
            }
            catch (Exception /*ex*/) { /*sec.Exception(ex);*/ }
        }

        public App()
        {
            var logger = DeferredLoggerFactory.CreateLogger<App>();
            using var activity = DeferredActivitySource.StartMethodActivity(logger);

            //var logger = Host.Services.GetRequiredService<ILogger<App>>();
            //using var activity = ActivitySource.StartMethodActivity(logger, new { });

        }
        protected override async void OnStartup(StartupEventArgs e)
        {
            var logger = DeferredLoggerFactory.CreateLogger<App>();
            using var activity = DeferredActivitySource.StartMethodActivity(logger);

            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<App>()
                .Build();
            logger.LogDebug($"var configuration = new ConfigurationBuilder()....Build() comleted");
            logger.LogDebug("environment:{environment},configuration:{Configuration}", environment, configuration);

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(builder =>
                {
                    using var innerActivity = DeferredActivitySource.StartRichActivity(logger, "ConfigureAppConfiguration.Callback", new { builder });

                    builder.Sources.Clear();
                    builder.AddConfiguration(configuration);
                    builder.AddUserSecrets<App>();
                    builder.AddEnvironmentVariables();
                }).ConfigureServices((context, services) =>
                {
                    using var innerActivity = DeferredActivitySource.StartRichActivity(logger, "ConfigureServices.Callback", new { context, services });
                    services.TryAddSingleton(DeferredActivitySource);
                    services.FlushOnCreateServiceProvider(DeferredLoggerFactory);

                    ConfigureServices(context.Configuration, services);
                })
                .ConfigureLogging((context, loggingBuilder) =>
                {
                    using var innerActivity = DeferredActivitySource.StartRichActivity(logger, "ConfigureLogging.Callback", new { context, loggingBuilder });

                    loggingBuilder.AddConfiguration(context.Configuration.GetSection("Logging"));
                    loggingBuilder.ClearProviders();
                    //var classConfigurationGetter = new ClassConfigurationGetter<App>(context.Configuration);
                    //var appInsightKey = classConfigurationGetter.Get(CONFIGVALUE_APPINSIGHTSKEY, DEFAULTVALUE_APPINSIGHTSKEY);

                    var services = loggingBuilder.Services;
                    services.AddLogging(
                                 loggingBuilder =>
                                 {
                                     loggingBuilder.ClearProviders();

                                     if (configuration.GetValue("AppSettings:ConsoleProviderEnabled", true))
                                     {
                                         loggingBuilder.AddDiginsightConsole();
                                     }

                                     if (configuration.GetValue("AppSettings:Log4NetProviderEnabled", true))
                                     {
                                         //loggingBuilder.AddDiginsightLog4Net("log4net.config");
                                         loggingBuilder.AddDiginsightLog4Net(static sp =>
                                         {
                                             IHostEnvironment env = sp.GetRequiredService<IHostEnvironment>();
                                             string fileBaseDir = env.IsDevelopment()
                                                     ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify)
                                                     : $"{Path.DirectorySeparatorChar}home";

                                             return new IAppender[]
                                                    {
                                                            new RollingFileAppender()
                                                            {
                                                                File = Path.Combine(fileBaseDir, "LogFiles", "Diginsight", typeof(App).Namespace!),
                                                                AppendToFile = true,
                                                                StaticLogFileName = false,
                                                                RollingStyle = RollingFileAppender.RollingMode.Composite,
                                                                DatePattern = @".yyyyMMdd.\l\o\g",
                                                                MaxSizeRollBackups = 1000,
                                                                MaximumFileSize = "100MB",
                                                                LockingModel = new FileAppender.MinimalLock(),
                                                                Layout = new DiginsightLayout()
                                                                {
                                                                    Pattern = "{Timestamp} {Category} {LogLevel} {TraceId} {Delta} {Duration} {Depth} {Indentation|-1} {Message}",
                                                                },
                                                            },
                                                    };
                                         },
                                         static _ => log4net.Core.Level.All);
                                     }
                                 }
                             );

                    services.ConfigureClassAware<DiginsightActivitiesOptions>(configuration.GetSection("Diginsight:Activities"));
                    services.AddSingleton<App>();

                })
                .UseDiginsightServiceProvider()
                .Build();

            logger.LogDebug("host = appBuilder.Build(); completed");
            await Host.StartAsync(); logger.LogDebug($"await Host.StartAsync();");

            var mainWindow = Host.Services.GetRequiredService<MainWindow>(); logger.LogDebug($"Host.Services.GetRequiredService<MainWindow>(); returns {mainWindow.ToLogString()}");

            mainWindow.Show(); logger.LogDebug($"mainWindow.Show();");
            base.OnStartup(e); logger.LogDebug($"base.OnStartup(e);");


        }
        private void ConfigureServices(IConfiguration configuration, IServiceCollection services)
        {
            using var activity = ActivitySource.StartMethodActivity(logger, new { configuration, services });

            //services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddHttpContextAccessor();
            //services.AddClassConfiguration();

            //var appSettingsSection = configuration.GetSection(nameof(AppSettings));
            //var settings = appSettingsSection.Get<AppSettings>();

            //services.AddApplicationInsightsTelemetry();
            //var aiConnectionString = configuration.GetValue<string>(Constants.APPINSIGHTSCONNECTIONSTRING);
            //services.AddObservability(configuration);

            services.AddSingleton<MainWindow>();

        }
        protected override async void OnExit(ExitEventArgs e)
        {
            using var activity = ActivitySource.StartMethodActivity(logger, new { e });

            using (Host)
            {
                await Host.StopAsync(TimeSpan.FromSeconds(5));
            }

            base.OnExit(e);
        }

        private string GetMethodName([CallerMemberName] string memberName = "") { return memberName; }


        // All the functions below simulate doing some arbitrary work
        static async Task DoSomeWork(string foo, int bar)
        {
            var logger = Host.Services.GetRequiredService<ILogger<App>>();
            using var activity = ActivitySource.StartMethodActivity(logger, new { foo, bar });

            await StepOne();
            await StepTwo();
        }

        static async Task StepOne()
        {
            var logger = Host.Services.GetRequiredService<ILogger<App>>();
            using var activity = ActivitySource.StartMethodActivity(logger, new { });

            await Task.Delay(500);
        }

        static async Task StepTwo()
        {
            var logger = Host.Services.GetRequiredService<ILogger<App>>();
            using var activity = ActivitySource.StartMethodActivity(logger, new { });

            await Task.Delay(1000);
        }
    }
}
