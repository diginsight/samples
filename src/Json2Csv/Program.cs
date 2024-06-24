namespace Json2Csv;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Diginsight;
using Diginsight.Diagnostics;
using Diginsight.Diagnostics.Log4Net;
using System.Text.Json;
using System;
using log4net.Appender;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Cocona;

internal class Program
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly IConfiguration configuration;
    private readonly IFileProvider fileProvider;

    private static readonly JsonSerializerOptions MyJsonSerializerOptions = new(JsonSerializerOptions.Default) { ReadCommentHandling = JsonCommentHandling.Skip };
    public static IHost host;
    private IProvisioner? provisioner;

    private IProvisioner Provisioner => provisioner ??= new Provisioner(loggerFactory.CreateLogger<Provisioner>());

    public Program(ILogger<Program> logger,
                   IConfiguration configuration,
                   ILoggerFactory loggerFactory,
                   IHostEnvironment hostEnvironment)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.loggerFactory = loggerFactory;
        fileProvider = hostEnvironment.ContentRootFileProvider;

        using var activity = Observability.ActivitySource.StartMethodActivity(logger);
        try
        {
            this.loggerFactory = loggerFactory;
            this.logger = logger;
            this.configuration = configuration;
            this.fileProvider = fileProvider;
        }
        catch (Exception /*ex*/) { /*sec.Exception(ex);*/ }
    }

    private static Task Main(string[] args)
    {
        var activitiesOptions = new DiginsightActivitiesOptions() { LogActivities = true };
        var deferredLoggerFactory = new DeferredLoggerFactory(activitiesOptions: activitiesOptions);
        deferredLoggerFactory.ActivitySources.Add(Observability.ActivitySource);
        var logger = deferredLoggerFactory.CreateLogger<Program>();

        CoconaApp app;
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { args });
        {
            var appBuilder = CoconaApp.CreateBuilder(args);

            logger.LogDebug("Configuration");
            appBuilder.Configuration
                      .AddJsonFile("appsettings.json")
                      .AddUserSecrets(typeof(Program).Assembly)
                      .AddEnvironmentVariables();

            IServiceCollection services = appBuilder.Services;
            //services.Configure<GraphClientCredentialsOptions>(appBuilder.Configuration.GetSection("GraphClient"));
            //services.Configure<ArmClientCredentialsCollection>(appBuilder.Configuration.GetSection("ArmClients"));

            IConfiguration configuration = appBuilder.Configuration;

            services.AddLogging(
                loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();

                    if (configuration.GetValue("AppSettings:ConsoleProviderEnabled", true))
                    {
                        loggingBuilder.AddDiginsightConsole();
                    }

                    if (configuration.GetValue("AppSettings:Log4NetProviderEnabled", false))
                    {
                        //loggingBuilder.AddDiginsightLog4Net("log4net.config");
                        services.AddLogging(loggingBuilder =>
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
                                            File = Path.Combine(fileBaseDir, "LogFiles", "Diginsight", typeof(Program).Namespace!),
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
                                static _ => log4net.Core.Level.All
                            );
                            }
                        }
                        );
                    }
                }
            );

            services.ConfigureClassAware<DiginsightActivitiesOptions>(configuration.GetSection("Diginsight:Activities"));

            logger.LogDebug("Other services");
            services.AddSingleton<Program>();

            services.FlushOnCreateServiceProvider(deferredLoggerFactory);
            appBuilder.Host.UseDiginsightServiceProvider();

            logger.LogDebug("App");
            app = appBuilder.Build();

            Program program = app.Services.GetRequiredService<Program>();

            app.AddCommand("convert", program.ConvertJsonFileAsync);
            //app.AddCommand("assign-roles", program.AssignRolesAsync);
            //app.AddCommand("assign-kv-policies", program.AssignKvAccessPoliciesAsync);

        }

        var task = app.RunAsync();
        return task;
    }

    private async Task ConvertJsonFileAsync(
        [Option('f', Description = "appsettings file")] string file,
        [Option('o', Description = "output csv file")] string outputFile,
        [Option('s', Description = "separator")] string separator,
        CoconaAppContext appContext)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { file, outputFile, separator });

        CancellationToken cancellationToken = appContext.CancellationToken;

        //const string fullPath = @"D:\Projects\ABB\EL-BackOffice-Backend\ABB.Ability.EL.BackOffice.Api\appsettings.json";
        JObject root = JObject.Parse(File.ReadAllText(file));
        var configurationEntries = new Dictionary<string, string>();

        Flatten(root, configurationEntries, null);
    }


    private void Flatten(JToken jtoken, Dictionary<string, string> configurationEntries, string? prefix)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { jtoken, prefix });

        switch (jtoken)
        {
            case JObject jobject:
                {
                    foreach (JProperty jprop in jobject.Properties())
                    {
                        Flatten(jprop.Value, configurationEntries, prefix is null ? jprop.Name : $"{prefix}:{jprop.Name}");
                    }
                    break;
                }
            case JArray jarray:
                {
                    foreach ((JToken element, int index) in jarray.Select((element, index) => (element, index)))
                    {
                        Flatten(element, configurationEntries, prefix is null ? index.ToString() : $"{prefix}:{index}");
                    }
                    break;
                }
            case JValue jvalue:
                {
                    //Console.Write('"');
                    //Console.Write(prefix!.Replace("\"", "\"\""));
                    //Console.Write("\";\"");

                    var key = prefix!.Replace("\"", "\"\"");
                    object? value = jvalue.Value;
                    string? valueString = value is null ? null : value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString();

                    configurationEntries.Add(key, valueString);

                    //Console.Write(valueString is null ? "" : valueString.Replace("\"", "\"\""));
                    //Console.WriteLine('"');
                    break;
                }
        }
    }

}
