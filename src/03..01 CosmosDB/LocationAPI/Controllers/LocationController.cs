using Diginsight.Diagnostics;
using LocationAPI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Collections.Generic;
using LocationAPI;
using System.Net.Http;
using Diginsight.Components;

namespace LocationAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class LocationController : ControllerBase
{
    private static readonly string[] Summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };

    private readonly ILogger<LocationController> logger;
    private readonly CosmosDbOptions locationsCosmosDBOptions;
    private readonly CosmosClient locationsCosmosClient;
    private readonly IHttpClientFactory httpClientFactory;
    private HttpClient identityApiHttpClient;

    public LocationController(
        ILogger<LocationController> logger,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;

        this.locationsCosmosDBOptions = serviceProvider.GetRequiredService<IOptionsMonitor<CosmosDbOptions>>().Get("LocationApi:CosmosDb");
        this.locationsCosmosClient = new CosmosClient(locationsCosmosDBOptions.ConnectionString); logger.LogDebug("cosmosClient = new CosmosClient(connectionString);");

        this.identityApiHttpClient = httpClientFactory.CreateClient("IdentityApi");
    }

    [HttpGet("locations/{type}")]
    public async Task<IEnumerable<LocationBase>> GetAllLocationsAsync(string type)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { type });

        var response = await this.identityApiHttpClient.SendAsync(HttpMethod.Get, "User/users", null, "User/users", HttpContext.RequestAborted); // api/

        var container = locationsCosmosClient.GetContainer(locationsCosmosDBOptions.Database, locationsCosmosDBOptions.Collection); logger.LogDebug($"container = cosmosClient.GetContainer({locationsCosmosDBOptions.Database}, {locationsCosmosDBOptions.Collection});");
        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.Type = @type")
                                                 .WithParameter("@type", type);

        var iterator = container.GetItemQueryIterator<LocationBase>(queryDefinition);
        var result = await iterator.GetItemsAsync();

        activity?.SetOutput(result);
        return result;
    }

    [HttpGet("countries")]
    public async Task<IEnumerable<LocationBase>> GetAllCountriesAsync()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        var container = locationsCosmosClient.GetContainer(locationsCosmosDBOptions.Database, locationsCosmosDBOptions.Collection); logger.LogDebug($"container = cosmosClient.GetContainer({locationsCosmosDBOptions.Database}, {locationsCosmosDBOptions.Collection});");

        var type = "Country";
        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.Type = @type")
                                                 .WithParameter("@type", type);

        var iterator = container.GetItemQueryIterator<LocationBase>(queryDefinition);
        var result = await iterator.GetItemsAsync();

        activity?.SetOutput(result);
        return result;
    }
}
