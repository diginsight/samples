
using Diginsight;
using Diginsight.Components;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Diginsight.Stringify;
using Microsoft.Extensions.Options;

namespace LocationAPI;

public class Program
{
    public static void Main(string[] args)
    {
        using var observabilityManager = new ObservabilityManager();
        ILoggerFactory loggerFactory = Observability.LoggerFactory = observabilityManager.LoggerFactory;
        ILogger logger = loggerFactory.CreateLogger(typeof(Program));

        WebApplication app = default!;
        using (var activity = Observability.ActivitySource.StartMethodActivity(logger, new { args }))
        {
            var builder = WebApplication.CreateBuilder(args); logger.LogDebug($"WebApplication.CreateBuilder({args.Stringify()});");
            builder.Host.ConfigureAppConfiguration2(observabilityManager.LoggerFactory); logger.LogDebug("builder.Host.ConfigureAppConfiguration2(observabilityManager.LoggerFactory);");

            var services = builder.Services;
            var configuration = builder.Configuration;

            services.AddObservability(observabilityManager, builder.Configuration, builder.Environment); logger.LogDebug("services.AddObservability(observabilityManager, builder.Configuration, builder.Environment);");

            services.Configure<CosmosDbOptions>("LocationApi:CosmosDb", builder.Configuration.GetSection("LocationApi:CosmosDb"));

            services
                   .Configure<AuthenticatedClientOptions>("IdentityApi", configuration.GetSection("IdentityApi"))
                   .Configure<HttpClientOptions>("IdentityApi", configuration.GetSection("IdentityApi"))
                   .AddHttpClient("IdentityApi")
                   .ConfigureHttpClient(
                       static (sp, hc) =>
                       {
                           IHttpClientOptions httpClientOptions = sp.GetRequiredService<IOptionsMonitor<HttpClientOptions>>().Get("IdentityApi");
                           hc.BaseAddress = httpClientOptions.BaseUrl;
                       }
                   )
                   .AddApplicationPermissionAuthentication()
                   .AddBodyLoggingHandler();

            // Add services to the container.
            services.AddControllers(); logger.LogDebug("services.AddControllers();");
            services.AddEndpointsApiExplorer(); logger.LogDebug("services.AddEndpointsApiExplorer();");
            services.AddSwaggerGen(); logger.LogDebug("services.AddSwaggerGen();");

            builder.UseDiginsightServiceProvider(true); logger.LogDebug("builder.UseDiginsightServiceProvider(true);");
            app = builder.Build(); logger.LogDebug("app = builder.Build();");

            // Configure the HTTP request pipeline.
            var isDevelopment = app.Environment.IsDevelopment(); 
            logger.LogDebug("isDevelopment: {isDevelopment};", isDevelopment);
            if (isDevelopment)
            {
                app.UseSwagger(); logger.LogDebug("app.UseSwagger();");
                app.UseSwaggerUI(); logger.LogDebug("app.UseSwaggerUI();");
            }

            app.UseHttpsRedirection(); logger.LogDebug("app.UseHttpsRedirection();");
            app.UseAuthorization(); logger.LogDebug("app.UseAuthorization();");
            app.MapControllers(); logger.LogDebug("app.MapControllers();");
        }

        logger.LogDebug("before app.Run();");
        app.Run(); logger.LogDebug("app.Run();");
    }
}
