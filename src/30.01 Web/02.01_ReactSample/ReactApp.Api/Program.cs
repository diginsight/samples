using Diginsight;
using Diginsight.AspNetCore;
using Diginsight.Components;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;

namespace ReactApp.Api;

public class Program
{
    public static void Main(string[] args)
    {
        using var observabilityManager = new ObservabilityManager();
        ILogger logger = observabilityManager.LoggerFactory.CreateLogger(typeof(Program));

        WebApplication app;
        using (var activity = Observability.ActivitySource.StartMethodActivity(logger, new { args }))
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.ConfigureAppConfiguration2(observabilityManager.LoggerFactory);

            IServiceCollection services = builder.Services;
            IConfiguration configuration = builder.Configuration;
            IWebHostEnvironment environment = builder.Environment;

            // Diginsight telemetry integrated with OpenTelemetry
            services.AddAspNetCoreObservability(configuration, environment, out IOpenTelemetryOptions openTelemetryOptions);
            observabilityManager.AttachTo(services);
            services.AddHttpObservability(openTelemetryOptions);

            services.TryAddSingleton<EarlyLoggingManager>(observabilityManager);
            services.AddHttpContextAccessor();
            services.AddDynamicLogLevel<DefaultDynamicLogLevelInjector>();

            // Protect the API with the 'samples-testmc-appreg-02' app registration (AzureAd section)
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));
            services.AddAuthorization();

            IdentityModelEventSource.ShowPII = true;

            // Allow the React SPA (ReactApp.Client) served by the Vite dev server to call the API
            string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:5173", "https://localhost:5173"];
            services.AddCors(options =>
            {
                options.AddPolicy("ReactClient", policy =>
                {
                    policy
                        .WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            services.AddControllers()
                    .AddControllersAsServices();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            services.AddOpenApi();

            builder.UseDiginsightServiceProvider(true);

            app = builder.Build();
            logger.LogDebug("Host built");

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors("ReactClient");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }
        }

        app.Run();
    }
}
