
using Diginsight;
using Diginsight.Components.Configuration;
using Diginsight.Diagnostics;
using Diginsight.Stringify;

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

            builder.Services.AddObservability(observabilityManager, builder.Configuration, builder.Environment); logger.LogDebug("builder.Services.AddObservability(observabilityManager, builder.Configuration, builder.Environment);");

            // Add services to the container.
            builder.Services.AddControllers(); logger.LogDebug("builder.Services.AddControllers();");
            builder.Services.AddEndpointsApiExplorer(); logger.LogDebug("builder.Services.AddEndpointsApiExplorer();");
            builder.Services.AddSwaggerGen(); logger.LogDebug("builder.Services.AddSwaggerGen();");

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
