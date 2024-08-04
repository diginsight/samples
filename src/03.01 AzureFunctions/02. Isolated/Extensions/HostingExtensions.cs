using Azure.Monitor.OpenTelemetry.Exporter;
using Diginsight;
using Diginsight.CAOptions;
using Diginsight.Diagnostics;
using Diginsight.Diagnostics.Log4Net;
using log4net.Appender;
using log4net.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Isolatedsample;

public static class HostingExtensions
{
    public static IHostBuilder ConfigureAppConfigurationNH(
        this IHostBuilder hostBuilder,
        IKeyVaultCredentialProvider? kvCredentialProvider = null,
        Func<IDictionary<string, string>, bool>? kvTagsMatch = null,
        IKeyVaultSecretNameParser? kvNameParser = null
    )
    {
        return hostBuilder.ConfigureAppConfiguration(
            (hbc, cb) => ConfigureAppConfigurationNH(hbc.HostingEnvironment, cb, kvCredentialProvider, kvTagsMatch, kvNameParser)
        );
    }

    public static void ConfigureAppConfigurationNH(
        IHostEnvironment environment,
        IConfigurationBuilder builder,
        IKeyVaultCredentialProvider? kvCredentialProvider = null,
        Func<IDictionary<string, string>, bool>? kvTagsMatch = null,
        IKeyVaultSecretNameParser? kvNameParser = null
    )
    {
        bool isLocal = environment.IsDevelopment();
        IList<IConfigurationSource> sources = builder.Sources;

        int? GetSourceIndex(Func<(IConfigurationSource Source, int Index), bool> predicate)
        {
            return sources
                .Select(static (source, index) => (Source: source, Index: index))
                .Where(predicate)
                .Select(static x => (int?)x.Index)
                .LastOrDefault();
        }

        int GetJsonSourceIndex(string path)
        {
            return GetSourceIndex(x => x.Source is JsonConfigurationSource jsonSource && string.Equals(jsonSource.Path, path, StringComparison.OrdinalIgnoreCase))
                ?? -1;
        }

        void AppendLocalJsonSource(string path, int index)
        {
            if (!isLocal) { return; }

            JsonConfigurationSource jsonSource = new()
            {
                Path = path,
                Optional = true,
                ReloadOnChange = true,
            };
            sources.Insert(index + 1, jsonSource);
        }

        int appsettingsIndex = GetJsonSourceIndex("appsettings.json");
        if (appsettingsIndex == -1) { appsettingsIndex = 1; }
        AppendLocalJsonSource("appsettings.local.json", appsettingsIndex);
        int appsettingsLocalIndex = GetJsonSourceIndex("appsettings.local.json");

        string hostingEnvironmentName = environment.EnvironmentName;
        string? appsettingsEnvName = Environment.GetEnvironmentVariable("AppsettingsEnvironmentName");
        int appsettingsHostingEnvIndex = GetJsonSourceIndex($"appsettings.{hostingEnvironmentName}.json");
        if (string.IsNullOrEmpty(appsettingsEnvName))
        {
            appsettingsEnvName = hostingEnvironmentName;
        }
        else
        {
            if (appsettingsHostingEnvIndex >= 0)
            {
                JsonConfigurationSource appsettingsEnvSource = (JsonConfigurationSource)sources[appsettingsHostingEnvIndex];
                sources.RemoveAt(appsettingsHostingEnvIndex);
                appsettingsEnvSource.Path = $"appsettings.{appsettingsEnvName}.json";
                sources.Insert(appsettingsHostingEnvIndex, appsettingsEnvSource);
            }
        }

        int appsettingsEnvIndex = GetJsonSourceIndex($"appsettings.{appsettingsEnvName}.json");
        if (appsettingsEnvIndex <= 0) { appsettingsEnvIndex = appsettingsLocalIndex; }
        if (appsettingsEnvIndex <= 0) { appsettingsEnvIndex = appsettingsIndex; }
        if (appsettingsEnvIndex <= 0) { appsettingsEnvIndex = 1; }
        if (appsettingsEnvIndex >= 0)
        {
            AppendLocalJsonSource($"appsettings.{appsettingsEnvName}.local.json", appsettingsEnvIndex);
        }

        IConfiguration configuration = builder.Build();
        if ((kvCredentialProvider ?? DefaultKeyVaultCredentialProvider.Default).Get(configuration, environment) is var (kvUri, kvCredential))
        {
            builder.AddAzureKeyVault(kvUri, kvCredential, new NHKeyVaultSecretManager(kvTagsMatch, kvNameParser));
        }

        int environmentVariablesIndex = GetSourceIndex(static x => x.Source is EnvironmentVariablesConfigurationSource) ?? -1;
        if (environmentVariablesIndex >= 0)
        {
            int sourcesCount = sources.Count;
            IConfigurationSource kvConfigurationSource = sources.Last();
            sources.RemoveAt(sourcesCount - 1);
            sources.Insert(environmentVariablesIndex, kvConfigurationSource);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        bool configureDefaults = true,
        TraceInstrumentationCallbacks? traceInstrumentationCallbacks = null
    )
    {
        return services.AddObservability(configuration, hostEnvironment, out _, configureDefaults, traceInstrumentationCallbacks);
    }

    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        out OpenTelemetryOptions mutableOpenTelemetryOptions,
        bool configureDefaults = true,
        TraceInstrumentationCallbacks? traceInstrumentationCallbacks = null
    )
    {
        traceInstrumentationCallbacks ??= new TraceInstrumentationCallbacks();

        const string diginsightConfKey = "Diginsight";
        const string observabilityConfKey = "Observability";

        bool isLocal = hostEnvironment.IsDevelopment();
        string assemblyName = Assembly.GetEntryAssembly()!.GetName().Name!;

        IConfiguration openTelemetryConfiguration = configuration.GetSection("OpenTelemetry");

        mutableOpenTelemetryOptions = new OpenTelemetryOptions();
        IOpenTelemetryOptions openTelemetryOptions = mutableOpenTelemetryOptions;
        if (configureDefaults)
        {
            void ConfigureOpenTelemetryDefaults(OpenTelemetryOptions o)
            {
                o.EnableTraces = true;
                o.EnableMetrics = true;
                o.TracingSamplingRatio = isLocal ? 1 : 0.1;
            }

            ConfigureOpenTelemetryDefaults(mutableOpenTelemetryOptions);
            services.Configure<OpenTelemetryOptions>(ConfigureOpenTelemetryDefaults);
        }

        openTelemetryConfiguration.Bind(mutableOpenTelemetryOptions);
        services.Configure<OpenTelemetryOptions>(openTelemetryConfiguration);

        services.TryAddSingleton<IActivityLoggingSampler, NameBasedActivityLoggingSampler>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IActivityListenerRegistration, ActivitySourceDetectorRegistration>());

        string? azureMonitorConnectionString = openTelemetryOptions.AzureMonitorConnectionString;

        services.AddLogging(
            loggingBuilder =>
            {
                loggingBuilder.ClearProviders();

                if (configuration.GetValue(ConfigurationPath.Combine(observabilityConfKey, "ConsoleEnabled"), true))
                {
                    loggingBuilder.AddDiginsightConsole(
                        fo =>
                        {
                            if (configureDefaults)
                            {
                                fo.TotalWidth = isLocal ? -1 : 0;
                            }
                            configuration.GetSection(ConfigurationPath.Combine(diginsightConfKey, "Console")).Bind(fo);
                        }
                    );
                }

                if (configuration.GetValue(ConfigurationPath.Combine(observabilityConfKey, "Log4NetEnabled"), false))
                {
                    loggingBuilder.AddDiginsightLog4Net(
                        sp =>
                        {
                            IHostEnvironment env = sp.GetRequiredService<IHostEnvironment>();
                            string fileBaseDir = env.IsDevelopment()
                                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify)
                                : $"{Path.DirectorySeparatorChar}home";

                            return new IAppender[]
                            {
                                new RollingFileAppender()
                                {
                                    File = Path.Combine(fileBaseDir, "LogFiles", "Diginsight", assemblyName),
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
                        static _ => Level.All
                    );
                }

                if (!string.IsNullOrEmpty(azureMonitorConnectionString))
                {
                    loggingBuilder.AddDiginsightOpenTelemetry(
                        otlo => otlo.AddAzureMonitorLogExporter(
                            exporterOptions => { exporterOptions.ConnectionString = azureMonitorConnectionString; }
                        )
                    );
                }
            }
        );

        if (configureDefaults)
        {
            services.Configure<DiginsightActivitiesOptions>(
                dao =>
                {
                    dao.LogActivities = true;
                    dao.MeterName = assemblyName;
                }
            );

            services.AddSingleton<IConfigureClassAwareOptions<DiginsightActivitiesOptions>>(
                new ConfigureClassAwareOptions<DiginsightActivitiesOptions>(
                    Options.DefaultName,
                    static (t, dao) =>
                    {
                        IReadOnlyList<string> markers = ClassConfigurationMarkers.For(t);
                        if (markers.Contains("ABB.*") || markers.Contains("Diginsight.*"))
                        {
                            dao.RecordSpanDurations = true;
                        }
                    }
                )
            );
        }

        services.ConfigureClassAware<DiginsightActivitiesOptions>(configuration.GetSection(ConfigurationPath.Combine(diginsightConfKey, "Activities")));

        IOpenTelemetryBuilder openTelemetryBuilder = services.AddDiginsightOpenTelemetry();

        if (openTelemetryOptions.EnableMetrics)
        {
            services.TryAddSingleton<ISpanDurationMetricRecorderSettings, NameBasedSpanDurationMetricRecorderSettings>();
            services.AddSpanDurationMetricRecorder();

            openTelemetryBuilder.WithMetrics(
                meterProviderBuilder =>
                {
                    meterProviderBuilder
                        .AddDiginsight()
                        .AddRuntimeInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddMeter(openTelemetryOptions.Meters.ToArray());

                    if (!string.IsNullOrEmpty(azureMonitorConnectionString))
                    {
                        meterProviderBuilder.AddAzureMonitorMetricExporter(
                            exporterOptions => { exporterOptions.ConnectionString = azureMonitorConnectionString; }
                        );
                    }
                }
            );
        }

        if (openTelemetryOptions.EnableTraces)
        {
            openTelemetryBuilder.WithTracing(
                tracerProviderBuilder =>
                {
                    tracerProviderBuilder
                        .AddDiginsight()
                        .AddHttpClientInstrumentation(
                            options =>
                            {
                                options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                                {
                                    if (httpRequestMessage.Content is not { } content)
                                        return;

                                    byte[] contentByteArray = content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                                    int contentLength = contentByteArray.Length;
                                    if (contentLength <= 0)
                                        return;

                                    activity.SetTag("http.request_content_length", contentLength);
                                    if (traceInstrumentationCallbacks.ShouldTagWithRequestContent(activity, httpRequestMessage))
                                    {
                                        activity.SetTag("http.response_content", Encoding.UTF8.GetString(contentByteArray));
                                    }
                                };

                                options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                                {
                                    byte[] contentByteArray = httpResponseMessage.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                                    int contentLength = contentByteArray.Length;
                                    activity.SetTag("http.response_content_length", contentLength);
                                    if (!httpResponseMessage.IsSuccessStatusCode && contentLength > 0 &&
                                        traceInstrumentationCallbacks.ShouldTagWithResponseContent(activity, httpResponseMessage))
                                    {
                                        activity.SetTag("http.response_content", Encoding.UTF8.GetString(contentByteArray));
                                    }
                                };

                                options.EnrichWithException = (activity, exception) =>
                                {
                                    if (traceInstrumentationCallbacks.ShouldTagWithStackTrace(activity, exception))
                                    {
                                        activity.SetTag("stack_trace", exception.StackTrace);
                                    }
                                };

                                options.FilterHttpRequestMessage = httpRequestMessage =>
                                {
                                    string requestHost = httpRequestMessage.RequestUri!.Host;

                                    foreach (string excludedHost in openTelemetryOptions.ExcludedHttpHosts)
                                    {
                                        if (excludedHost[0] == '.' && requestHost.EndsWith(excludedHost, StringComparison.Ordinal))
                                            return false;
                                        if (requestHost == excludedHost)
                                            return false;
                                    }

                                    return true;
                                };
                            }
                        )
                        .AddSource(openTelemetryOptions.ActivitySources.ToArray())
                        .SetErrorStatusOnException();

                    if (!string.IsNullOrEmpty(azureMonitorConnectionString))
                    {
                        tracerProviderBuilder.AddAzureMonitorTraceExporter(
                            exporterOptions => { exporterOptions.ConnectionString = azureMonitorConnectionString; }
                        );
                    }
                }
            );
        }

        return services;
    }

    private sealed class ActivitySourceDetector : IActivityListenerLogic
    {
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<string, ValueTuple> seenActivitySources = new();

        public ActivitySourceDetector(ILogger<ActivitySourceDetector> logger)
        {
            this.logger = logger;
        }

        public void ActivityStarted(Activity activity)
        {
            string activitySourceName = activity.Source.Name;

            if (seenActivitySources.TryAdd(activitySourceName, default))
            {
                logger.LogDebug("New activity source detected: {ActivitySource}", activitySourceName);
            }
        }
    }

    private sealed class ActivitySourceDetectorRegistration : IActivityListenerRegistration
    {
        public IActivityListenerLogic Logic { get; }

        public ActivitySourceDetectorRegistration(IServiceProvider serviceProvider)
        {
            Logic = ActivatorUtilities.CreateInstance<ActivitySourceDetector>(serviceProvider);
        }

        public bool ShouldListenTo(ActivitySource activitySource) => true;
    }
}
