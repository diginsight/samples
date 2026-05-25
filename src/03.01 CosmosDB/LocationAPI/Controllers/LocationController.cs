using Azure.Identity;
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
using Diginsight.Components.Azure;

namespace LocationAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class LocationController : ControllerBase
{
    private static readonly Type T = typeof(LocationController);
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
        this.locationsCosmosClient = CreateCosmosClient(locationsCosmosDBOptions.ConnectionString, logger); logger.LogDebug("cosmosClient = CreateCosmosClient(connectionString);");

        this.identityApiHttpClient = httpClientFactory.CreateClient("IdentityApi");
    }

    /// <summary>
    /// Create a CosmosClient supporting both classic AccountKey connection strings
    /// and AAD-only endpoints. The <paramref name="connectionString"/> may be:
    ///   - a full "AccountEndpoint=...;AccountKey=...;" connection string, OR
    ///   - just a https:// endpoint URL (then DefaultAzureCredential is used), OR
    ///   - "AccountEndpoint=...;" without AccountKey (then DefaultAzureCredential is used).
    /// </summary>
    private static CosmosClient CreateCosmosClient(string connectionString, ILogger logger)
    {
        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(static x => x.Split('=', 2))
            .Where(static x => x.Length == 2)
            .ToDictionary(static x => x[0].Trim(), static x => x[1].Trim(), StringComparer.OrdinalIgnoreCase);

        string? accountEndpoint = null;
        if (parts.TryGetValue("AccountEndpoint", out var ep))
        {
            accountEndpoint = ep;
        }
        else if (Uri.IsWellFormedUriString(connectionString.Trim(), UriKind.Absolute))
        {
            accountEndpoint = connectionString.Trim();
        }

        bool hasKey = parts.ContainsKey("AccountKey");

        if (!hasKey)
        {
            if (string.IsNullOrWhiteSpace(accountEndpoint))
            {
                throw new ArgumentException("Connection string has no AccountEndpoint and is not a bare endpoint URL.", nameof(connectionString));
            }
            logger.LogInformation("Using AAD auth (DefaultAzureCredential) for endpoint {endpoint}", accountEndpoint);
            return new CosmosClient(accountEndpoint, new DefaultAzureCredential());
        }

        logger.LogDebug("Using AccountKey auth for endpoint {endpoint}", accountEndpoint);
        return new CosmosClient(connectionString);
    }

    [HttpGet("locations/{type}")]
    public async Task<IEnumerable<LocationBase>> GetAllLocationsAsync(string type)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { type });

        var response = await this.identityApiHttpClient.SendAsync(HttpMethod.Get, "User/users", null, "User/users", HttpContext.RequestAborted); // api/

        var container = locationsCosmosClient.GetContainer(locationsCosmosDBOptions.Database, locationsCosmosDBOptions.Collection); logger.LogDebug($"container = cosmosClient.GetContainer({locationsCosmosDBOptions.Database}, {locationsCosmosDBOptions.Collection});");
        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.Type = @type")
                                                 .WithParameter("@type", type);

        logger.LogDebug("CosmosDB query for class '{Type}' in database {Endpoint}, collection '{Collection}'", T, container.Database.Client.Endpoint, container.Id);
        logger.LogTrace("Query: {Query}", queryDefinition.QueryText);

        var iterator = container.GetItemQueryIteratorObservable<LocationBase>(queryDefinition);
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

        logger.LogDebug("CosmosDB query for class '{Type}' in database {Endpoint}, collection '{Collection}'", T, container.Database.Client.Endpoint, container.Id);
        logger.LogTrace("Query: {Query}", queryDefinition.ToString());

        var iterator = container.GetItemQueryIteratorObservable<LocationBase>(queryDefinition);
        var result = await iterator.GetItemsAsync();

        activity?.SetOutput(result);
        return result;
    }
}
