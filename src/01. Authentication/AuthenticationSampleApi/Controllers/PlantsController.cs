#region using
using Asp.Versioning;
using Asp.Versioning.Http;
using Diginsight.Components;
using Diginsight.Diagnostics;
using Diginsight.Logging;
using Diginsight.Options;
using Diginsight.SmartCache;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OpenTelemetry.Trace; 
#endregion

namespace AuthenticationSampleApi
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "common")]
    public class PlantsController : ControllerBase
    {
        private readonly Type T = typeof(PlantsController);
        private readonly ILogger<PlantsController> logger;
        private readonly IClassAwareOptionsMonitor<FeatureFlagOptions> featureFlagsOptionsMonitor;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ISmartCache smartCache;
        private readonly ICacheKeyService cacheKeyService;

        private HttpClient authenticationSampleServerApiHttpClient;

        public PlantsController(
            TracerProvider tracerProvider,
            ILogger<PlantsController> logger,
            IClassAwareOptionsMonitor<FeatureFlagOptions> featureFlagsOptionsMonitor,
            IHttpClientFactory httpClientFactory,
            ISmartCache smartCache,
            ICacheKeyService cacheKeyService)
        {
            this.logger = logger;
            this.featureFlagsOptionsMonitor = featureFlagsOptionsMonitor;
            this.httpClientFactory = httpClientFactory;
            this.smartCache = smartCache;
            this.cacheKeyService = cacheKeyService;

            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            this.authenticationSampleServerApiHttpClient = httpClientFactory.CreateClient("AuthenticationSampleServerApi");
        }

        [HttpGet("getplantsimpl", Name = nameof(GetPlantsImplAsync))]
        [ApiVersion(ApiVersions.V_2024_04_26.Name)]
        public async Task<IEnumerable<Plant>> GetPlantsImplAsync()
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            var result = default(IEnumerable<Plant>);

            var latency = 1000;
            Thread.Sleep(latency); logger.LogDebug("Thread.Sleep({latency});", latency); // Structured logging
            logger.LogDebug($"Thread.Sleep({latency});"); // interpolation

            var plantsString = await System.IO.File.ReadAllTextAsync("Content/plants.json");
            var plants = JsonConvert.DeserializeObject<IEnumerable<Plant>>(plantsString);

            activity?.SetOutput(plants);
            return plants ?? [];
        }

        [HttpGet("getplantbyidimpl/{id}", Name = nameof(GetPlantByIdImplAsync))]
        [ApiVersion(ApiVersions.V_2024_04_26.Name)]
        public async Task<Plant?> GetPlantByIdImplAsync([FromRoute] Guid id)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { id });

            var result = default(IEnumerable<Plant>);

            Thread.Sleep(1000);

            var plants = await GetPlantsAsync();

            var plant = plants.FirstOrDefault(p => p.Id == id);

            activity?.SetOutput(plant);
            return plant;
        }


        [HttpGet("getplants", Name = nameof(GetPlantsAsync))]
        [ApiVersion(ApiVersions.V_2024_04_26.Name)]
        public async Task<IEnumerable<Plant>> GetPlantsAsync()
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            // get users from SampleServerApi
            var response = await this.authenticationSampleServerApiHttpClient.SendAsync(HttpMethod.Get, "api/Users/getUsers", null, "Users/getUsers", HttpContext.RequestAborted);

            var options = new SmartCacheOperationOptions() { MaxAge = TimeSpan.FromMinutes(10) };
            var cacheKey = new MethodCallCacheKey(cacheKeyService, typeof(PlantsController), nameof(GetPlantsAsync));
            var plants = await smartCache.GetAsync(cacheKey, _ => GetPlantsImplAsync(), options);

            activity?.SetOutput(plants);
            return plants ?? [];
        }

        [HttpGet("getplantbyid/{plantId}", Name = nameof(GetPlantByIdAsync))]
        [ApiVersion(ApiVersions.V_2024_04_26.Name)]
        public async Task<Plant?> GetPlantByIdAsync([FromRoute] Guid plantId)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { plantId });

            var options = new SmartCacheOperationOptions() { MaxAge = TimeSpan.FromMinutes(10) };
            var cacheKey = new MethodCallCacheKey(cacheKeyService, typeof(PlantsController), nameof(GetPlantByIdAsync), plantId);

            var plant = await smartCache.GetAsync(cacheKey, _ => GetPlantByIdImplAsync(plantId), options);

            activity?.SetOutput(plant);
            return plant;
        }


        [HttpPost("createorupdateplant", Name = nameof(CreateOrUpdatePlant))]
        [ApiVersion(ApiVersions.V_2024_04_26.Name)]
        public async Task<IEnumerable<Plant>> CreateOrUpdatePlant(Plant newplant)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger); // , new { foo, bar }

            var plants = (await GetPlantsImplAsync())?.ToList();

            var plant = plants?.FirstOrDefault(p => p.Id == newplant.Id);
            if (plant != null)
            {
                plant.Name = newplant.Name;
                plant.Description = newplant.Description;
                plant.Address = newplant.Address;
                plant.CreationDate = newplant.CreationDate;
            }
            else { plants?.Add(newplant); }

            var plantsString = JsonConvert.SerializeObject(plants);
            await System.IO.File.WriteAllTextAsync("Content/plants.json", plantsString);

            smartCache.Invalidate(new PlantInvalidationRule(newplant.Id)); logger.LogDebug($"smartCache.Invalidate(new PlantInvalidationRule({newplant.Id}));");

            activity?.SetOutput(plants);
            return plants ?? [];
        }
    }
}
