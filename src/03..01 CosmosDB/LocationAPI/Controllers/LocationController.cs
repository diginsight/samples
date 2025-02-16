using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace LocationAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class LocationController : ControllerBase
{
    private static readonly string[] Summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };

    private readonly ILogger<LocationController> logger;
    private readonly CosmosDbOptions locationCosmosDBOptions;

    public LocationController(
        ILogger<LocationController> logger,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;

        this.locationCosmosDBOptions = serviceProvider.GetRequiredService<IOptionsMonitor<CosmosDbOptions>>().Get("LocationCosmosDbOptions");

    }

    [HttpGet(Name = "GetAllLocations")]
    public IEnumerable<WeatherForecast> GetAll()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        var cosmosClient = new CosmosClient(locationCosmosDBOptions.ConnectionString); logger.LogDebug("cosmosClient = new CosmosClient(connectionString);");
        var container = cosmosClient.GetContainer(locationCosmosDBOptions.Database, locationCosmosDBOptions.Collection); logger.LogDebug($"container = cosmosClient.GetContainer({locationCosmosDBOptions.Database}, {locationCosmosDBOptions.Collection});");



        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }
}
