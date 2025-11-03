using Diginsight;
using Diginsight.Components;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Diginsight.Stringify;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace IdentityAPI;

public class Program
{
    public static void Main(string[] args)
    {
        using var observabilityManager = new ObservabilityManager();
        ILoggerFactory loggerFactory = Observability.LoggerFactory = observabilityManager.LoggerFactory;
        ObservabilityRegistry.RegisterLoggerFactory(observabilityManager.LoggerFactory);
        ILogger logger = loggerFactory.CreateLogger(typeof(Program));

        WebApplication app = default!;
        using (var activity = Observability.ActivitySource.StartMethodActivity(logger, new { args }))
        {
            var builder = WebApplication.CreateBuilder(args); logger.LogDebug($"WebApplication.CreateBuilder({args.Stringify()});");
            builder.Host.ConfigureAppConfiguration2(observabilityManager.LoggerFactory); logger.LogDebug("builder.Host.ConfigureAppConfiguration2(observabilityManager.LoggerFactory);");

            var services = builder.Services;
            var environment = builder.Environment;
            var configuration = builder.Configuration; 
            services.AddObservability(observabilityManager, configuration, environment); logger.LogDebug("services.AddObservability(observabilityManager, configuration, environment);");

            services.Configure<CosmosDbOptions>("IdentityApi:CosmosDb", builder.Configuration.GetSection("IdentityApi:CosmosDb"));

            services.Configure<AuthenticatedClientOptions>("LocationApi", configuration.GetSection("LocationApi"))
                    .Configure<HttpClientOptions>("LocationApi", configuration.GetSection("LocationApi"))
                    .AddHttpClient("LocationApi")
                    .ConfigureHttpClient(
                        static (sp, hc) =>
                        {
                            IHttpClientOptions httpClientOptions = sp.GetRequiredService<IOptionsMonitor<HttpClientOptions>>().Get("LocationApi");
                            hc.BaseAddress = httpClientOptions.BaseUrl;
                        }
                    )
                    .AddApplicationPermissionAuthentication()
                    .AddBodyLoggingHandler();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(configuration); logger.LogDebug("app.AddAuthentication().AddMicrosoftIdentityWebApi(configuration);");

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
            app.UseAuthentication(); logger.LogDebug("app.UseAuthentication();");
            app.UseAuthorization(); logger.LogDebug("app.UseAuthorization();");
            app.MapControllers(); logger.LogDebug("app.MapControllers();");
        }

        logger.LogDebug("before app.Run();");
        app.Run(); logger.LogDebug("app.Run();");
    }
}
