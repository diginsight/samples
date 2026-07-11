using Diginsight;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Diginsight.Stringify;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;
using Microsoft.Identity.Web.TokenCacheProviders.InMemory;
using Azure.Data.Tables;
using Azure.Identity;
using System.Text.Json;
//using TableStorageSampleAPI.Repositories;
using Diginsight.Components.Azure.Extensions;

namespace TableStorageSampleAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using var observabilityManager = new ObservabilityManager();
            ILoggerFactory loggerFactory = LoggerFactoryStaticAccessor.LoggerFactory = observabilityManager.LoggerFactory;
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
                
                // Add services to the container.
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
                logger.LogDebug("app.AddAuthentication().AddMicrosoftIdentityWebApi(configuration);");

                // Add token cache provider (required for Microsoft Graph)
                builder.Services.AddInMemoryTokenCaches();
                logger.LogDebug("services.AddInMemoryTokenCaches();");

                // Add Microsoft Graph services
                builder.Services.AddMicrosoftGraph();
                logger.LogDebug("services.AddMicrosoftGraph();");

                // Add Azure Table Storage services
                // A bare https:// table endpoint uses Entra ID (DefaultAzureCredential);
                // a full connection string / "UseDevelopmentStorage=true" uses key/emulator auth.
                builder.Services.AddSingleton<TableServiceClient>(_ =>
                    CreateTableServiceClient(configuration.GetConnectionString("AzureStorage"), logger));
                logger.LogDebug("services.AddSingleton<TableServiceClient>();");

                // Add Repository services - using generic repository with extension method
                builder.Services.AddAzureTableRepository<SampleAzureTableRecord>("SampleTable");
                logger.LogDebug("services.AddAzureTableRepository<SampleAzureTableRecord>(\"SampleTable\");");

                // Example: Register repositories for other entity types using the extension method
                // builder.Services.AddAzureTableRepository<ProductRecord>("ProductsTable");
                // builder.Services.AddAzureTableRepository<UserProfileRecord>("UserProfilesTable");
                
                // Alternative: Register multiple repositories at once
                // builder.Services.AddAzureTableRepositories(
                //     new[] { typeof(SampleAzureTableRecord), typeof(ProductRecord), typeof(UserProfileRecord) },
                //     "{0}Table"); // Pattern: EntityNameTable

                builder.Services.AddControllers()
                    .AddJsonOptions(options =>
                    {
                        // Configure JSON options for API responses to use camelCase by default
                        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                        options.JsonSerializerOptions.WriteIndented = true;
                    });
                logger.LogDebug("services.AddControllers() with JSON options");
                
                // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
                builder.Services.AddEndpointsApiExplorer(); logger.LogDebug("services.AddEndpointsApiExplorer();");
                builder.Services.AddSwaggerGen(); logger.LogDebug("services.AddSwaggerGen();");

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

        /// <summary>
        /// Create a <see cref="TableServiceClient"/> supporting both classic connection strings
        /// and AAD-only endpoints. The <paramref name="connectionString"/> may be:
        ///   - a full "DefaultEndpointsProtocol=...;AccountName=...;AccountKey=...;" connection string, OR
        ///   - "UseDevelopmentStorage=true" for the local Azurite/Storage emulator, OR
        ///   - just a https:// table endpoint URL (then DefaultAzureCredential is used).
        /// </summary>
        private static TableServiceClient CreateTableServiceClient(string? connectionString, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Use development storage emulator if no connection string is provided
                connectionString = "UseDevelopmentStorage=true";
            }

            var trimmed = connectionString.Trim();
            if ((trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                && Uri.IsWellFormedUriString(trimmed, UriKind.Absolute))
            {
                logger.LogInformation("Using AAD auth (DefaultAzureCredential) for table endpoint {endpoint}", trimmed);
                return new TableServiceClient(new Uri(trimmed), new DefaultAzureCredential());
            }

            logger.LogDebug("Using connection-string auth for Azure Table Storage");
            return new TableServiceClient(connectionString);
        }
    }
}
