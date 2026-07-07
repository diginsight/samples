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

            // Swagger UI + OpenAPI document are exposed in Development and can be toggled off
            // via the IsSwaggerEnabled setting in appsettings.json.
            bool swaggerEnabled = app.Environment.IsDevelopment()
                && configuration.GetValue("IsSwaggerEnabled", true);

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

            // Map the OpenAPI document and Swagger UI before the SPA fallback below, otherwise the
            // extensionless "/swagger" GET is captured by the fallback and returns the Blazor shell.
            if (swaggerEnabled)
            {
                app.MapOpenApi();

                // The document is served (relative to the path base) at "openapi/v1.json". The UI
                // page loads at "{pathBase}/swagger/index.html", so use a URL relative to it
                // ("../openapi/v1.json") to resolve to "{pathBase}/openapi/v1.json" under any path base.
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("../openapi/v1.json", "BlazorApp.Api v1");
                });
            }

            // Serve the Blazor WebAssembly client (BlazorApp.Client) hosted by this API.
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseCors("BlazorClient");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            // SPA fallback: serve the Blazor shell for client-side routes only. Routing already
            // handled the API and the static/_framework files, so this runs for app routes alone.
            // The shell is read once at startup (with the fingerprinted boot script resolved), and
            // only its <base href> placeholder is filled per request from the current path base —
            // supplied by UsePathBase locally, or by IIS under a virtual application — so the client
            // boots under "/{pathBase}" without any request interception.
            string shellHtml = string.Empty;
            var shellFile = environment.WebRootFileProvider.GetFileInfo("index.html");
            if (shellFile.Exists)
            {
                using (var shellStream = shellFile.CreateReadStream())
                using (var shellReader = new StreamReader(shellStream))
                {
                    shellHtml = shellReader.ReadToEnd();
                }

                var bootFile = environment.WebRootFileProvider.GetDirectoryContents("_framework")
                    .FirstOrDefault(f => !f.IsDirectory
                        && f.Name.StartsWith("blazor.webassembly.", StringComparison.Ordinal)
                        && f.Name.EndsWith(".js", StringComparison.Ordinal));
                if (bootFile is not null)
                {
                    shellHtml = shellHtml.Replace(
                        "_framework/blazor.webassembly#[.{fingerprint}].js",
                        "_framework/" + bootFile.Name);
                }
            }

            app.MapFallback(async context =>
            {
                string basePath = context.Request.PathBase.HasValue
                    ? context.Request.PathBase.Value!.TrimEnd('/') + "/"
                    : "/";
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(
                    shellHtml.Replace("<base href=\"/\" />", $"<base href=\"{basePath}\" />"));
            });
        }

        app.Run();
    }
}
