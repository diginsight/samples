using Asp.Versioning;
using Diginsight.Diagnostics;
using Diginsight.Logging;
using Diginsight.Options;
using Diginsight.SmartCache;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OpenTelemetry.Trace;

namespace AuthenticationSampleServerApi
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "common")]
    public class UsersController : ControllerBase
    {
        private readonly Type T = typeof(UsersController);
        private readonly ILogger<UsersController> logger;
        private readonly IClassAwareOptionsMonitor<FeatureFlagOptions> featureFlagsOptionsMonitor;
        private readonly ISmartCache smartCache;
        private readonly ICacheKeyService cacheKeyService;

        public UsersController(
            TracerProvider tracerProvider,
            ILogger<UsersController> logger,
            IClassAwareOptionsMonitor<FeatureFlagOptions> featureFlagsOptionsMonitor,
            ISmartCache smartCache,
            ICacheKeyService cacheKeyService)
        {
            this.logger = logger;
            this.featureFlagsOptionsMonitor = featureFlagsOptionsMonitor;
            this.smartCache = smartCache;
            this.cacheKeyService = cacheKeyService;

            using var activity = Observability.ActivitySource.StartMethodActivity(logger); // , new { foo, bar }
        
        
        
        }

        [HttpGet("getusersimpl", Name = nameof(GetUsersImplAsync))]
        [ApiVersion(ApiVersions.V_2024_04_26.Name)]
        public async Task<IEnumerable<User>> GetUsersImplAsync()
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger); // , new { foo, bar }

            var result = default(IEnumerable<User>);

            var latency = 1000;
            Thread.Sleep(latency); logger.LogDebug("Thread.Sleep({latency});", latency); // Structured logging
            logger.LogDebug($"Thread.Sleep({latency});"); // interpolation
            // logger.LogDebug(() => $"Thread.Sleep({latency});"); // interpolation with delegate notation
            // logger.LogDebug(new { result }); // variables loggin

            // read string usersString from content file /Content/users.json
            var usersString = await System.IO.File.ReadAllTextAsync("Content/users.json");
            var users = JsonConvert.DeserializeObject<IEnumerable<User>>(usersString);

            activity?.SetOutput(users);
            return users;
        }

        [HttpGet("getuserbyidimpl/{id}", Name = nameof(GetUserByIdImplAsync))]
        [ApiVersion(ApiVersions.V_2024_04_26.Name)]
        public async Task<User> GetUserByIdImplAsync([FromRoute] Guid id)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { id });

            var result = default(IEnumerable<User>);

            Thread.Sleep(1000);

            var users = await GetUsersAsync();

            var user = users.FirstOrDefault(p => p.Id == id);

            activity?.SetOutput(user);
            return user;
        }


        [HttpGet("getusers", Name = nameof(GetUsersAsync))]
        [ApiVersion(ApiVersions.V_2024_04_26.Name)]
        public async Task<IEnumerable<User>> GetUsersAsync()
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            var options = new SmartCacheOperationOptions() { MaxAge = TimeSpan.FromMinutes(10) };
            var cacheKey = new MethodCallCacheKey(cacheKeyService, typeof(UsersController), nameof(GetUsersAsync));

            var users = await smartCache.GetAsync(cacheKey, _ => GetUsersImplAsync(), options);

            activity?.SetOutput(users);
            return users;
        }

        [HttpGet("getuserbyid/{userId}", Name = nameof(GetUserByIdAsync))]
        [ApiVersion(ApiVersions.V_2024_04_26.Name)]
        public async Task<User> GetUserByIdAsync([FromRoute] Guid userId)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            var options = new SmartCacheOperationOptions() { MaxAge = TimeSpan.FromMinutes(10) };
            var cacheKey = new MethodCallCacheKey(cacheKeyService, typeof(UsersController), nameof(GetUserByIdAsync), userId);

            var user = await smartCache.GetAsync(cacheKey, _ => GetUserByIdImplAsync(userId), options);

            activity?.SetOutput(user);
            return user;
        }


        [HttpPost("createorupdateuser", Name = nameof(CreateOrUpdateUser))]
        [ApiVersion(ApiVersions.V_2024_04_26.Name)]
        public async Task<IEnumerable<User>> CreateOrUpdateUser(User newuser)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger); // , new { foo, bar }

            var users = (await GetUsersImplAsync())?.ToList();

            var user = users?.FirstOrDefault(p => p.Id == newuser.Id);
            if (user != null)
            {
                user.Name = newuser.Name;
                user.Surname = newuser.Surname;
                user.Email = newuser.Email;
                user.CreationDate = newuser.CreationDate;
            }
            else { users?.Add(newuser); }

            var usersString = JsonConvert.SerializeObject(users);
            await System.IO.File.WriteAllTextAsync("Content/users.json", usersString);

            smartCache.Invalidate(new UserInvalidationRule(newuser.Id)); logger.LogDebug($"smartCache.Invalidate(new UserInvalidationRule({newuser.Id}));");

            activity?.SetOutput(users);
            return users;
        }
    }
}
