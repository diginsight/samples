using System.IO;
using System.IO;
using Diginsight.Diagnostics;
using Diginsight.Diagnostics.Log4Net;
using log4net.Appender;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

[assembly: FunctionsStartup(typeof(Microsoft.Azure.Functions.Samples.DependencyInjectionBasic.SampleStartup))]

namespace Microsoft.Azure.Functions.Samples.DependencyInjectionBasic
{
    public class SampleStartup : FunctionsStartup
    {
        public static ILoggerFactory LoggerFactory;
        private readonly ILogger logger;

        //private static readonly JsonSerializerOptions MyJsonSerializerOptions = new(JsonSerializerOptions.Default) { ReadCommentHandling = JsonCommentHandling.Skip };
        //public static IHost host;
        //private readonly IConfiguration configuration;
        //private readonly IFileProvider fileProvider;

        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            //var activitiesOptions = new DiginsightActivitiesOptions() { LogActivities = true };
            //var deferredLoggerFactory = new DeferredLoggerFactory(activitiesOptions: activitiesOptions);
            //deferredLoggerFactory.ActivitySources.Add(Observability.ActivitySource);
            //LoggerFactory = deferredLoggerFactory;
            //var logger = LoggerFactory.CreateLogger<SampleStartup>();
            //using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { builder });

            FunctionsHostBuilderContext context = builder.GetContext();

            builder.ConfigurationBuilder
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, "appsettings.json"), optional: true, reloadOnChange: false)
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, $"appsettings.{context.EnvironmentName}.json"), optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .AddUserSecrets<SampleStartup>();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var logger = LoggerFactory.CreateLogger<SampleStartup>();
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { builder });

            FunctionsHostBuilderContext context = builder.GetContext();
            var configuration = context.Configuration;

            builder.Services.AddScoped<IGreeter, SampleGreeter>();
            
            builder.Services.Configure<SampleOptions>(context.Configuration);

            //builder.Services.AddLogging(loggingBuilder =>
            //{
            //    using var innerActivity = Observability.ActivitySource.StartRichActivity(logger, "ConfigureLogging.Callback", new { context, loggingBuilder });

            //    loggingBuilder.AddConfiguration(context.Configuration.GetSection("Logging"));
            //    loggingBuilder.ClearProviders();

            //    var services = loggingBuilder.Services;
            //    services.AddLogging(
            //                 loggingBuilder =>
            //                 {
            //                     loggingBuilder.ClearProviders();

            //                     if (configuration.GetValue("AppSettings:ConsoleProviderEnabled", true))
            //                     {
            //                         loggingBuilder.AddDiginsightConsole();
            //                     }

            //                     if (configuration.GetValue("AppSettings:Log4NetProviderEnabled", true))
            //                     {
            //                         //loggingBuilder.AddDiginsightLog4Net("log4net.config");
            //                         loggingBuilder.AddDiginsightLog4Net(static sp =>
            //                         {
            //                             IHostEnvironment env = sp.GetRequiredService<IHostEnvironment>();
            //                             string fileBaseDir = env.IsDevelopment()
            //                                         ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify)
            //                                         : $"{Path.DirectorySeparatorChar}home";

            //                             return new IAppender[]
            //                             {
            //                                new RollingFileAppender()
            //                                {
            //                                    File = Path.Combine(fileBaseDir, "LogFiles", "Diginsight", typeof(SampleStartup).Namespace!),
            //                                    AppendToFile = true,
            //                                    StaticLogFileName = false,
            //                                    RollingStyle = RollingFileAppender.RollingMode.Composite,
            //                                    DatePattern = @".yyyyMMdd.\l\o\g",
            //                                    MaxSizeRollBackups = 1000,
            //                                    MaximumFileSize = "100MB",
            //                                    LockingModel = new FileAppender.MinimalLock(),
            //                                    Layout = new DiginsightLayout()
            //                                    {
            //                                        Pattern = "{Timestamp} {Category} {LogLevel} {TraceId} {Delta} {Duration} {Depth} {Indentation|-1} {Message}",
            //                                    },
            //                                },
            //                            };
            //                         },
            //                         static _ => log4net.Core.Level.All);
            //                     }
            //                 }
            //             );

            //});
        }

    }
}
