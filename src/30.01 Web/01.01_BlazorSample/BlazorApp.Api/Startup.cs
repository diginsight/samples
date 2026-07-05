using Diginsight;
using Diginsight.AspNetCore;
using Diginsight.Components;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;

namespace BlazorApp.Api;

public class Startup
{
    private readonly EarlyLoggingManager observabilityManager;
    private readonly ILoggerFactory loggerFactory;
    private readonly IConfiguration configuration;
    private readonly IHostEnvironment hostEnvironment;

    public Startup(IConfiguration configuration, IHostEnvironment hostEnvironment, EarlyLoggingManager observabilityManager)
    {
        this.configuration = configuration;
        this.hostEnvironment = hostEnvironment;
        this.observabilityManager = observabilityManager;
        loggerFactory = observabilityManager.LoggerFactory;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        var logger = loggerFactory.CreateLogger<Startup>();
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { services });

        // Diginsight telemetry integrated with OpenTelemetry
        services.AddAspNetCoreObservability(configuration, hostEnvironment, out IOpenTelemetryOptions openTelemetryOptions);
        observabilityManager.AttachTo(services);
        services.AddHttpObservability(openTelemetryOptions);

        services.AddHttpContextAccessor();
        services.AddDynamicLogLevel<DefaultDynamicLogLevelInjector>();

        // Protect the API with the 'samples-testmc-appreg-02' app registration (AzureAd section)
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));
        services.AddAuthorization();

        IdentityModelEventSource.ShowPII = true;

        // Allow the Blazor WebAssembly client (BlazorApp.Client) to call the API
        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["https://localhost:7259", "http://localhost:5049"];
        services.AddCors(options =>
        {
            options.AddPolicy("BlazorClient", policy =>
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
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        var logger = loggerFactory.CreateLogger<Startup>();
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { app, env });

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseCors("BlazorClient");

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            if (env.IsDevelopment())
            {
                endpoints.MapOpenApi();
            }
        });
    }
}
