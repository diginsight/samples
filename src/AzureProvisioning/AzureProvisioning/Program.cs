using Cocona;
using Diginsight;
using Diginsight.Diagnostics;
using Diginsight.Diagnostics.Log4Net;
using log4net.Appender;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AzureProvisioning.Configurations;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AzureProvisioning;

internal class Program
{
    private static readonly JsonSerializerOptions MyJsonSerializerOptions = new(JsonSerializerOptions.Default) { ReadCommentHandling = JsonCommentHandling.Skip };

    internal static ILoggerFactory LoggerFactory = default(LoggerFactory)!;

    private readonly ILogger logger;
    private readonly IHostEnvironment hostEnvironment;
    private readonly IConfiguration configuration;
    private readonly IFileProvider fileProvider;
    private readonly IGraphClientCredentialsOptions graphClientCredentialsOptions;
    private readonly IReadOnlyDictionary<string, IArmClientCredentialsOptions> armClientCredentialsOptionsDict;

    private IProvisioner? provisioner;
    private IProvisioner Provisioner => provisioner ??= new Provisioner(
                                                                configuration["TenantId"]!,
                                                                LoggerFactory.CreateLogger<Provisioner>(),
                                                                graphClientCredentialsOptions,
                                                                armClientCredentialsOptionsDict,
                                                                hostEnvironment);

    //IHostEnvironment

    public Program(
        ILogger<Program> logger,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        IOptions<GraphClientCredentialsOptions> graphClientCredentialsOptions,
        IOptions<ArmClientCredentialsCollection> armClientCredentialsCollectionOptions
    )
    {
        this.logger = logger;
        this.configuration = configuration;
        this.hostEnvironment = hostEnvironment;

        fileProvider = hostEnvironment.ContentRootFileProvider;
        this.graphClientCredentialsOptions = graphClientCredentialsOptions.Value;
        armClientCredentialsOptionsDict = armClientCredentialsCollectionOptions.Value;
    }

    private static Task Main(string[] args)
    {
        var activitiesOptions = new DiginsightActivitiesOptions() { LogActivities = true };
        var deferredLoggerFactory = new DeferredLoggerFactory(activitiesOptions: activitiesOptions);
        LoggerFactory = deferredLoggerFactory;
        deferredLoggerFactory.ActivitySources.Add(Observability.ActivitySource);
        var logger = deferredLoggerFactory.CreateLogger<Program>();

        CoconaApp app;
        using (Observability.ActivitySource.StartMethodActivity(logger, new { args }))
        {
            var appBuilder = CoconaApp.CreateBuilder(args); logger.LogDebug("var appBuilder = CoconaApp.CreateBuilder(args);");

            logger.LogDebug("Build Configuration");
            appBuilder.Configuration.AddJsonFile("appsettings.json")
                                    .AddUserSecrets(typeof(Program).Assembly)
                                    .AddEnvironmentVariables();

            IServiceCollection services = appBuilder.Services;
            services.Configure<GraphClientCredentialsOptions>(appBuilder.Configuration.GetSection("GraphClient"));
            services.Configure<ArmClientCredentialsCollection>(appBuilder.Configuration.GetSection("ArmClients"));

            IConfiguration configuration = appBuilder.Configuration;

            services.AddLogging(
                loggingBuilder =>
                {
                    loggingBuilder.ClearProviders();

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
                        static _ => log4net.Core.Level.All
                    );
                }
            );

            services.ConfigureClassAware<DiginsightActivitiesOptions>(configuration.GetSection("Diginsight:Activities"));

            services.AddSingleton<Program>(); logger.LogDebug("services.AddSingleton<Program>();");

            services.FlushOnCreateServiceProvider(deferredLoggerFactory); logger.LogDebug("services.FlushOnCreateServiceProvider(deferredLoggerFactory);");
            appBuilder.Host.UseDiginsightServiceProvider(); logger.LogDebug("appBuilder.Host.UseDiginsightServiceProvider();");

            app = appBuilder.Build(); logger.LogDebug("app = appBuilder.Build();");

            var program = app.Services.GetRequiredService<Program>(); logger.LogDebug("var program = app.Services.GetRequiredService<Program>();");

            app.AddCommand("ensure-groups", program.EnsureGroupsAsync); logger.LogDebug("app.AddCommand(\"ensure-groups\", program.EnsureGroupsAsync);");
            app.AddCommand("fill-groups", program.FillGroupsAsync); logger.LogDebug("app.AddCommand(\"fill-groups\", program.FillGroupsAsync);");
            app.AddCommand("assign-roles", program.AssignRolesAsync); logger.LogDebug("app.AddCommand(\"assign-roles\", program.AssignRolesAsync);");
            app.AddCommand("assign-kv-policies", program.AssignKvAccessPoliciesAsync); logger.LogDebug("app.AddCommand(\"assign-kv-policies\", program.AssignKvAccessPoliciesAsync);");
        }

        logger.LogDebug("before app.RunAsync();");
        return app.RunAsync();
    }

    private async Task EnsureGroupsAsync([Option('f')] string file, CoconaAppContext appContext)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { file });

        try
        {
            IReadOnlyDictionary<string, GroupDescriptor> groupDescriptors;
            await using (Stream stream = fileProvider.GetFileInfo(file).CreateReadStream())
            {
                groupDescriptors = (await JsonSerializer.DeserializeAsync<IReadOnlyDictionary<string, GroupDescriptor>>(
                    stream, MyJsonSerializerOptions, CancellationToken.None
                ))!;
            }

            await CoreEnsureGroupsAsync(groupDescriptors, appContext.CancellationToken);
        }
        catch (Exception e) { logger.LogError(e, e.Message); }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task CoreEnsureGroupsAsync(IReadOnlyDictionary<string, GroupDescriptor> groupDescriptors, CancellationToken cancellationToken)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { groupDescriptors });

        await Provisioner.EnsureGroupsAsync(groupDescriptors.ToDictionary(static x => x.Key, static x => x.Value.Owners), cancellationToken);
    }

    private async Task FillGroupsAsync([Option('f')] string file, [Option('x')] string? excessMembersFile, [Option('e')] bool ensure, CoconaAppContext appContext)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { file, excessMembersFile, ensure });

        CancellationToken cancellationToken = appContext.CancellationToken;

        IReadOnlyDictionary<string, GroupDescriptor> groupDescriptors;
        await using (Stream stream = fileProvider.GetFileInfo(file).CreateReadStream())
        {
            groupDescriptors = (await JsonSerializer.DeserializeAsync<IReadOnlyDictionary<string, GroupDescriptor>>(
                stream, MyJsonSerializerOptions, CancellationToken.None
            ))!;
        }

        if (ensure)
        {
            await CoreEnsureGroupsAsync(groupDescriptors, cancellationToken);
        }

        IDictionary<string, ISet<string>> excessMembersByGroup = new Dictionary<string, ISet<string>>();
        try
        {
            await Provisioner.FillGroupsAsync(
                groupDescriptors.ToDictionary(static x => x.Key, static x => x.Value.Members),
                excessMembersByGroup,
                cancellationToken
            );
        }
        finally
        {
            if (excessMembersFile is not null)
            {
                await using Stream stream = File.OpenWrite(fileProvider.GetFileInfo(excessMembersFile).PhysicalPath!);
                await using StreamWriter writer = new(stream);
                await Provisioner.WriteExcessMembersAsync(writer, excessMembersByGroup);
            }
        }
    }

    private async Task AssignRolesAsync([Option('f')] string file, CoconaAppContext appContext)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { file });

        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IEnumerable<string>>> roleAsgsByTarget;
        await using (Stream stream = fileProvider.GetFileInfo(file).CreateReadStream())
        {
            roleAsgsByTarget = (await JsonSerializer.DeserializeAsync<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IEnumerable<string>>>>(
                stream, MyJsonSerializerOptions, CancellationToken.None
            ))!;
        }

        await Provisioner.AssignRolesAsync(roleAsgsByTarget, appContext.CancellationToken);
    }

    private async Task AssignKvAccessPoliciesAsync([Option('f')] string file, CoconaAppContext appContext)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { file });

        IReadOnlyDictionary<string, IEnumerable<KvAccessDescriptor>> accessDescriptorsByKv;
        await using (Stream stream = fileProvider.GetFileInfo(file).CreateReadStream())
        {
            accessDescriptorsByKv = (await JsonSerializer.DeserializeAsync<IReadOnlyDictionary<string, IEnumerable<KvAccessDescriptor>>>(
                stream, MyJsonSerializerOptions, CancellationToken.None
            ))!;
        }

        await Provisioner.AssignKvAccessPoliciesAsync(accessDescriptorsByKv, appContext.CancellationToken);
    }
}
