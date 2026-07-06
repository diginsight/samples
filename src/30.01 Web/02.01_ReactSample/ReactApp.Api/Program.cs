using Diginsight;
using Diginsight.AspNetCore;
using Diginsight.Components;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
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

            // Host the SPA + API under a configurable virtual path (e.g. "/reactapp") so both are
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

            // Serve the built React SPA from a configurable folder so the SPA and API share a
            // single origin under "/{pathBase}".
            string spaRoot = configuration["AppHosting:SpaRoot"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(spaRoot))
            {
                // Published layout: the Vite build output is copied into the app's wwwroot.
                // Dev layout: the build output next to the ReactApp.Client project.
                string webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
                spaRoot = File.Exists(Path.Combine(webRoot, "index.html"))
                    ? webRoot
                    : Path.Combine(environment.ContentRootPath, "..", "ReactApp.Client", "dist");
            }
            spaRoot = Path.GetFullPath(spaRoot);

            IFileProvider? spaFileProvider = Directory.Exists(spaRoot) ? new PhysicalFileProvider(spaRoot) : null;
            if (spaFileProvider is null)
            {
                logger.LogWarning("React SPA root not found at {SpaRoot}; run 'npm run build' in ReactApp.Client. Serving API only.", spaRoot);
            }
            else
            {
                // Static assets (js/css/img) are public and served before authentication.
                app.UseStaticFiles(new StaticFileOptions { FileProvider = spaFileProvider });

                // Serve the SPA shell (index.html) for client-side navigation routes before auth so
                // the public app can load; the /api endpoints below stay protected. The configured
                // virtual path is injected: <base> resolves the relative Vite assets and
                // window.__BASE_PATH__ drives the React Router basename and the API base URL.
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

                    var indexFile = spaFileProvider.GetFileInfo("index.html");
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
                        ? context.Request.PathBase.Value!.TrimEnd('/')
                        : string.Empty;
                    string injection = $"<base href=\"{basePath}/\" /><script>window.__BASE_PATH__=\"{basePath}\";</script>";
                    html = html.Replace("<head>", "<head>" + injection);

                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync(html);
                });
            }

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
