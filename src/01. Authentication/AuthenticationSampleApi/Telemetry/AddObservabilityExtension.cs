#nullable enable

using Azure.Monitor.OpenTelemetry.Exporter;
using Diginsight;
using Diginsight.AspNetCore;
using Diginsight.CAOptions;
using Diginsight.Diagnostics;
using Diginsight.Diagnostics.AspNetCore;
using Diginsight.Diagnostics.Log4Net;
using log4net.Appender;
using log4net.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

namespace AuthenticationSampleApi;

public static class AddObservabilityExtension
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        IConfiguration openTelemetryConfiguration = configuration.GetSection("OpenTelemetry");
        OpenTelemetryOptions openTelemetryOptions = new();
        openTelemetryConfiguration.Bind(openTelemetryOptions);
        services.Configure<OpenTelemetryOptions>(openTelemetryConfiguration);

        services.TryAddSingleton<IActivityLoggingSampler, HttpHeadersActivityLoggingSampler>();
        services.Decorate<IActivityLoggingSampler, MyActivityLoggingSampler>();

        services.TryAddSingleton<ISpanDurationMetricRecorderSettings, HttpHeadersSpanDurationMetricRecorderSettings>();
        services.Decorate<ISpanDurationMetricRecorderSettings, MySpanDurationMetricRecorderSettings>();

        services.AddSpanDurationMetricRecorder();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IActivityListenerRegistration, ActivitySourceDetectorRegistration>());

        services.Configure<DiginsightDistributedContextOptions>(
            static x =>
            {
                x.NonBaggageKeys.Add(HttpHeadersActivityLoggingSampler.HeaderName);
                x.NonBaggageKeys.Add(HttpHeadersSpanDurationMetricRecorderSettings.HeaderName);
            }
        );

        var azureMonitorConnectionString = configuration["ApplicationInsights:ConnectionString"];

        services.AddLogging(
            loggingBuilder =>
            {
                loggingBuilder.ClearProviders();

                if (configuration.GetValue("AppSettings:ConsoleProviderEnabled", true))
                {
                    loggingBuilder.AddDiginsightConsole(configuration.GetSection("Diginsight:Console").Bind);
                }

                if (configuration.GetValue("AppSettings:Log4NetProviderEnabled", false))
                {
                    loggingBuilder.AddDiginsightLog4Net(
                        static sp =>
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
                        static _ => Level.All
                    );
                }

                if (!string.IsNullOrEmpty(azureMonitorConnectionString))
                {
                    loggingBuilder.AddOpenTelemetry(
                        otlo => otlo.AddAzureMonitorLogExporter(
                            exporterOptions => { exporterOptions.ConnectionString = azureMonitorConnectionString; }
                        )
                    );
                }
            }
        );

        services.ConfigureClassAware<DiginsightActivitiesOptions>(configuration.GetSection("Diginsight:Activities"));
        services.PostConfigureFromHttpRequestHeaders<DiginsightActivitiesOptions>();

        var builder = services.AddDiginsightOpenTelemetry();

        if (openTelemetryOptions.EnableMetrics)
        {
            builder.WithMetrics(
                meterProviderBuilder =>
                {
                    meterProviderBuilder
                        .AddDiginsight()
                        .AddAspNetCoreInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddMeter(openTelemetryOptions.Meters.ToArray())
                        .AddPrometheusExporter();

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
            builder.WithTracing(
                tracerProviderBuilder =>
                {
                    tracerProviderBuilder
                        .AddDiginsight()
                        .AddAspNetCoreInstrumentation(
                            static options =>
                            {
                                options.EnrichWithHttpRequest = static (activity, httpRequest) =>
                                {
                                    var context = httpRequest.HttpContext;

                                    activity.DisplayName = $"{context.Request.Method.ToUpperInvariant()} {context.Request.Path}";
                                    activity.AddTag("http.client_ip", context.Connection.RemoteIpAddress);
                                    activity.AddTag("http.request_content_length", httpRequest.ContentLength);
                                    activity.AddTag("http.request_content_type", httpRequest.ContentType);
                                };
                                options.EnrichWithHttpResponse = static (activity, httpResponse) =>
                                {
                                    activity.AddTag("http.response_content_length", httpResponse.ContentLength);
                                    activity.AddTag("http.response_content_type", httpResponse.ContentType);
                                };
                                options.EnrichWithException = static (activity, exception) => { activity.SetTag("stack_trace", exception.StackTrace); };
                            }
                        )
                        .AddHttpClientInstrumentation(
                            options =>
                            {
                                options.EnrichWithHttpRequestMessage = static (activity, httpRequestMessage) =>
                                {
                                    if (httpRequestMessage.Content is not null)
                                    {
                                        byte[] contentByteArray = httpRequestMessage.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                                        activity.AddTag("http.request_content", Encoding.UTF8.GetString(contentByteArray));
                                    }
                                };
                                options.EnrichWithHttpResponseMessage = static (activity, httpResponseMessage) =>
                                {
                                    byte[] contentByteArray = httpResponseMessage.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                                    int contentLength = contentByteArray.Length;
                                    activity.AddTag("http.response_content_length", contentLength);
                                    if (!httpResponseMessage.IsSuccessStatusCode && contentLength > 0)
                                    {
                                        activity.AddTag("http.response_content", Encoding.UTF8.GetString(contentByteArray));
                                    }
                                };
                                options.EnrichWithException = static (activity, exception) =>
                                {
                                    //activity.SetTag("stack_trace", exception.StackTrace);
                                    activity.SetTag("error", true);
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

                    tracerProviderBuilder
                        .SetHttpHeadersSampler(
                            static sp =>
                            {
                                OpenTelemetryOptions openTelemetryOptions = sp.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value;
                                return new ParentBasedSampler(new TraceIdRatioBasedSampler(openTelemetryOptions.TracingSamplingRatio));
                            }
                        );
                }
            );
        }

        return services;
    }

    private sealed class MyActivityLoggingSampler : NameBasedActivityLoggingSampler
    {
        private readonly IActivityLoggingSampler decoratee;

        public MyActivityLoggingSampler(IActivityLoggingSampler decoratee, IOptions<DiginsightActivitiesOptions> activitiesOptions)
            : base(activitiesOptions)
        {
            this.decoratee = decoratee;
        }

        public override bool? ShouldLog(Activity activity)
        {
            return decoratee.ShouldLog(activity) ?? base.ShouldLog(activity);
        }
    }

    private sealed class MySpanDurationMetricRecorderSettings : NameBasedSpanDurationMetricRecorderSettings
    {
        private readonly ISpanDurationMetricRecorderSettings decoratee;
        private readonly IClassAwareOptionsMonitor<OpenTelemetryOptions> openTelemetryOptionsMonitor;

        public MySpanDurationMetricRecorderSettings(
            ISpanDurationMetricRecorderSettings decoratee,
            IClassAwareOptionsMonitor<OpenTelemetryOptions> openTelemetryOptionsMonitor,
            IOptions<DiginsightActivitiesOptions> activitiesOptions
        )
            : base(activitiesOptions)
        {
            this.decoratee = decoratee;
            this.openTelemetryOptionsMonitor = openTelemetryOptionsMonitor;
        }

        public override bool? ShouldRecord(Activity activity)
        {
            return decoratee.ShouldRecord(activity) ?? base.ShouldRecord(activity);
        }

        public override IEnumerable<KeyValuePair<string, object?>> ExtractTags(Activity activity)
        {
            OpenTelemetryOptions openTelemetryOptions = openTelemetryOptionsMonitor.Get(activity.GetCallerType());
            return openTelemetryOptions.DurationMetricTags
                .Select(k => (Key: k, Value: activity.GetAncestors(true).Select(a => a.GetTagItem(k)).FirstOrDefault(static v => v is not null)))
                .Where(static x => x.Value is not null)
                .Select(static x => KeyValuePair.Create(x.Key, x.Value))
                .Concat(decoratee.ExtractTags(activity))
                .Concat(base.ExtractTags(activity));
        }
    }
}
