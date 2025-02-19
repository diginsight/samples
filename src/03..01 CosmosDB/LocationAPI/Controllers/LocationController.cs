using Diginsight.Diagnostics;
using LocationAPI;
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

        this.locationCosmosDBOptions = serviceProvider.GetRequiredService<IOptionsMonitor<CosmosDbOptions>>().Get("LocationApi:CosmosDb");

    }

    [HttpGet("locations/{type}")]
    public async Task<IEnumerable<LocationBase>> GetAllLocationsAsync(string type)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { type });

        var cosmosClient = new CosmosClient(locationCosmosDBOptions.ConnectionString); logger.LogDebug("cosmosClient = new CosmosClient(connectionString);");
        var container = cosmosClient.GetContainer(locationCosmosDBOptions.Database, locationCosmosDBOptions.Collection); logger.LogDebug($"container = cosmosClient.GetContainer({locationCosmosDBOptions.Database}, {locationCosmosDBOptions.Collection});");

        //var iterator = container.GetItemQueryIterator<LocationBase>(new QueryDefinition($"SELECT * FROM c WHERE c.Type = '{type}'"));
        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.Type = @type")
                              .WithParameter("@type", type);
        var iterator = container.GetItemQueryIterator<LocationBase>(queryDefinition);

        var result = new List<LocationBase>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            result.AddRange(response);
        }

        activity?.SetOutput(result);
        return result;
    }


    [HttpGet("countries")]
    public async Task<IEnumerable<WeatherForecast>> GetAllCountries()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        var cosmosClient = new CosmosClient(locationCosmosDBOptions.ConnectionString); logger.LogDebug("cosmosClient = new CosmosClient(connectionString);");
        var container = cosmosClient.GetContainer(locationCosmosDBOptions.Database, locationCosmosDBOptions.Collection); logger.LogDebug($"container = cosmosClient.GetContainer({locationCosmosDBOptions.Database}, {locationCosmosDBOptions.Collection});");

        // container.GetItemQueryIterator<LocationBase>

        // SELECT * FROM c WHERE c.Type = 'Country' // {type}
        // "Address",
        // "Country",
        // "Municipality",
        // "Site"


        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }


}
