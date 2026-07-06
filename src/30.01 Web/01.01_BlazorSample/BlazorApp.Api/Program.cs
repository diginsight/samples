using Diginsight;
using Diginsight.AspNetCore;
using Diginsight.Components;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;

namespace BlazorApp.Api;

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

            builder.UseDiginsightServiceProvider(true);

            app = builder.Build();
            logger.LogDebug("Host built");

            // Host the SPA + API under a configurable virtual path (e.g. "/blazorapp") so both are
            // served from a single origin, mirroring the single App Service deployment.
            string pathBase = (configuration["AppHosting:PathBase"] ?? string.Empty).Trim().Trim('/');
            if (!string.IsNullOrEmpty(pathBase))
            {
                app.UsePathBase("/" + pathBase);
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // Serve the Blazor WebAssembly client (BlazorApp.Client) hosted by this API.
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            // Serve the SPA shell (index.html) for client-side routes before authentication so the
            // public client can load; the /api endpoints below stay protected. Its <base href> is
            // rewritten to the configured virtual path so the WebAssembly client boots under "/{pathBase}".
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path;
                if (!HttpMethods.IsGet(context.Request.Method)
                    || path.StartsWithSegments("/api")
                    || Path.HasExtension(path.Value))
                {
                    await next();
                    return;
                }

                var indexFile = environment.WebRootFileProvider.GetFileInfo("index.html");
                if (!indexFile.Exists)
                {
                    await next();
                    return;
                }

                string html;
                await using (var stream = indexFile.CreateReadStream())
                using (var reader = new StreamReader(stream))
                {
                    html = await reader.ReadToEndAsync();
                }

                string basePath = context.Request.PathBase.HasValue
                    ? context.Request.PathBase.Value!.TrimEnd('/') + "/"
                    : "/";
                html = html.Replace("<base href=\"/\" />", $"<base href=\"{basePath}\" />");

                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(html);
            });

            app.UseRouting();

            app.UseCors("BlazorClient");

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
