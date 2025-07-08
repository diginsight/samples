#region using
using Diginsight;
using Diginsight.Components;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Diginsight.Diagnostics.Log4Net;
using Diginsight.Logging;
using Diginsight.Stringify;
using log4net.Appender;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
#endregion

namespace AuthenticationSampleClient
{
    /// <summary>Interaction logic for App.xaml</summary>
    public partial class App : Application
    {
        const string CONFIGVALUE_APPINSIGHTSKEY = "AppInsightsKey", DEFAULTVALUE_APPINSIGHTSKEY = "";
        public static ObservabilityManager ObservabilityManager;

        static Type T = typeof(App);
        public static IHost Host;
        //private ILogger<App> logger;

        static App()
        {
            ObservabilityManager = new ObservabilityManager();
            ILogger logger = ObservabilityManager.LoggerFactory.CreateLogger(typeof(App));

            using var activity = Observability.ActivitySource.StartMethodActivity(logger);
            try
            {

            }
            catch (Exception /*ex*/) { /*sec.Exception(ex);*/ }
        }

        public App()
        {
            var logger = ObservabilityManager.LoggerFactory.CreateLogger<App>();
            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            //var logger = Host.Services.GetRequiredService<ILogger<App>>();
            //using var activity = ActivitySource.StartMethodActivity(logger, new { });

        }
        protected override async void OnStartup(StartupEventArgs e)
        {
            var logger = ObservabilityManager.LoggerFactory.CreateLogger<App>();
            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
            //var configuration = new ConfigurationBuilder()
            //    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            //    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            //    .AddEnvironmentVariables()
            //    .AddUserSecrets<App>()
            //    .Build();

            //logger.LogDebug($"var configuration = new ConfigurationBuilder()....Build() comleted");
            //logger.LogDebug("environment:{environment},configuration:{Configuration}", environment, configuration);

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                            .ConfigureAppConfiguration2(ObservabilityManager.LoggerFactory, static tags => tags.ContainsKey("AppSettings"))
                //.ConfigureAppConfiguration(builder =>
                //{
                //    using var innerActivity = Observability.ActivitySource.StartRichActivity(logger, "ConfigureAppConfiguration.Callback", new { builder });

                //    builder.Sources.Clear();
                //    builder.AddConfiguration(configuration);
                //    builder.AddUserSecrets<App>();
                //    builder.AddEnvironmentVariables();
                //})
                .ConfigureServices((context, services) =>
                {
                    using var innerActivity = Observability.ActivitySource.StartRichActivity(logger, "ConfigureServices.Callback", new { context, services });

                    var configuration = context.Configuration;
                    var environment = context.HostingEnvironment;

                    services.ConfigureClassAware<DiginsightActivitiesOptions>(configuration.GetSection("Diginsight:Activities"));
                    ObservabilityManager.AttachTo(services);

                    services
                        .Configure<AuthenticatedClientOptions>("AuthenticationSampleApi", configuration.GetSection("AuthenticationSampleApi"))
                        .Configure<HttpClientOptions>("AuthenticationSampleApi", configuration.GetSection("AuthenticationSampleApi"))
                        .AddHttpClient("AuthenticationSampleApi")
                        .ConfigureHttpClient(
                            static (sp, hc) =>
                            {
                                IHttpClientOptions httpClientOptions = sp.GetRequiredService<IOptionsMonitor<HttpClientOptions>>().Get("AuthenticationSampleApi");
                                hc.BaseAddress = httpClientOptions.BaseUrl;
                            }
                        )
                        .AddApplicationPermissionAuthentication()
                        .AddBodyLoggingHandler();

                    ConfigureServices(context.Configuration, services);

                    services.AddSingleton<App>();
                })
                .ConfigureLogging((context, loggingBuilder) =>
                {
                    using var innerActivity = Observability.ActivitySource.StartRichActivity(logger, "ConfigureLogging.Callback", new { context, loggingBuilder });

                    var configuration = context.Configuration;

                    loggingBuilder.AddConfiguration(context.Configuration.GetSection("Logging"));
                    loggingBuilder.ClearProviders();

                    var services = loggingBuilder.Services;
                    services.AddLogging(
                                 loggingBuilder =>
                                 {
                                     loggingBuilder.ClearProviders();

                                     if (configuration.GetValue("Observability:ConsoleEnabled", true))
                                     {
                                         loggingBuilder.AddDiginsightConsole();
                                     }

                                     if (configuration.GetValue("Observability:Log4NetEnabled", true))
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
                                                                Encoding = System.Text.Encoding.UTF8,
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

                })
                .UseDiginsightServiceProvider(true)
                .Build();

            logger.LogDebug("host = appBuilder.Build(); completed");
            await Host.StartAsync(); logger.LogDebug($"await Host.StartAsync();");

            var mainWindow = Host.Services.GetRequiredService<MainWindow>(); logger.LogDebug($"Host.Services.GetRequiredService<MainWindow>(); returns {mainWindow.Stringify()}");

            mainWindow.Show(); logger.LogDebug($"mainWindow.Show();");
            base.OnStartup(e); logger.LogDebug($"base.OnStartup(e);");
        }
        
        private void ConfigureServices(IConfiguration configuration, IServiceCollection services)
        {
            var logger = ObservabilityManager.LoggerFactory.CreateLogger<App>();
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { configuration, services });

            //services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddHttpContextAccessor();

            services.ConfigureClassAware<AppSettingsOptions>(configuration.GetSection("AppSettings"));
            services.ConfigureClassAware<FeatureFlagOptions>(configuration.GetSection("AppSettings"));

            //services.ConfigureClassAware<AuthenticationSampleApiOptions>(configuration.GetSection("AuthenticationSampleApi"));
            services.ConfigureClassAware<AzureAdOptions>(configuration.GetSection("AzureAd"));

            services.AddHttpClient();
            services.AddResponseCompression();
            services.AddHttpContextAccessor();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();

            //services.AddApplicationInsightsTelemetry();
            //var aiConnectionString = configuration.GetValue<string>(Constants.APPINSIGHTSCONNECTIONSTRING);
            //services.AddObservability(configuration);

            services.AddSingleton<MainWindow>();

        }
        protected override async void OnExit(ExitEventArgs e)
        {
            var logger = ObservabilityManager.LoggerFactory.CreateLogger<App>();
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { e });

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
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { foo, bar });

            await StepOne();
            await StepTwo();
        }

        static async Task StepOne()
        {
            var logger = Host.Services.GetRequiredService<ILogger<App>>();
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { });

            await Task.Delay(500);
        }

        static async Task StepTwo()
        {
            var logger = Host.Services.GetRequiredService<ILogger<App>>();
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { });

            await Task.Delay(1000);
        }
    }
}
