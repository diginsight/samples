using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;
using System.Security.Claims;

namespace IdentityAPI.Controllers;

[Authorize]
[Route("[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private static readonly Type T = typeof(UserController);

    private readonly ILogger<UserController> logger;
    private readonly CosmosDbOptions identityCosmosDBOptions;
    private readonly CosmosClient identityCosmosClient;
    private readonly bool isNonProduction;

    public UserController(
        ILogger<UserController> logger,
        IServiceProvider serviceProvider
        )
    {
        this.logger = logger;
        this.identityCosmosDBOptions = serviceProvider.GetRequiredService<IOptionsMonitor<CosmosDbOptions>>().Get("IdentityApi:CosmosDb");
        this.identityCosmosClient = new CosmosClient(identityCosmosDBOptions.ConnectionString); logger.LogDebug("cosmosClient = new CosmosClient(connectionString);");

        IHostEnvironment hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
        var isNonProduction = hostEnvironment.IsDevelopment() ||
            Environment.GetEnvironmentVariable("AppsettingsEnvironmentName")?.StartsWith("prod", StringComparison.OrdinalIgnoreCase) == false;
    }

    [HttpGet("users")]
    [Authorize(Roles = "access_as_app")]
    public async Task<IEnumerable<User>> GetUsers()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);
        //CheckPermissions(["access_as_app"]);

        var container = identityCosmosClient.GetContainer(identityCosmosDBOptions.Database, identityCosmosDBOptions.Collection); 

        var type = "User";
        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.Type = @type")
        .WithParameter("@type", type);

        logger.LogDebug("CosmosDB query for class '{Type}' in database {Endpoint}, collection '{Collection}'", T, container.Database.Client.Endpoint, container.Id);
        logger.LogTrace("Query: {Query}", queryDefinition.ToString());

        var iterator = container.GetItemQueryIterator<User>(queryDefinition);
        var result = await iterator.GetItemsAsync();

        activity?.SetOutput(result);
        return result;
    }

    [HttpGet("userprofiles")]
    [Authorize(Roles = "access_as_app")]
    public async Task<IEnumerable<User>> GetUserProfiles()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        var container = identityCosmosClient.GetContainer(identityCosmosDBOptions.Database, identityCosmosDBOptions.Collection);

        var type = "User";
        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.Type = @type")
                                                 .WithParameter("@type", type);

        logger.LogDebug("CosmosDB query for class '{Type}' in database {Endpoint}, collection '{Collection}'", T, container.Database.Client.Endpoint, container.Id);
        logger.LogTrace("Query: {Query}", queryDefinition.ToString());

        var iterator = container.GetItemQueryIterator<User>(queryDefinition);
        var result = await iterator.GetItemsAsync();

        activity?.SetOutput(result);
        return result;
    }

    protected (bool IsDaemon, string[] Permissions) CheckPermissions(string[]? scopes = null, string[]? roles = null)
    {
        if (scopes is null && roles is null)
        {
            throw new ArgumentException($"At least one of {nameof(scopes)} and {nameof(roles)} must be provided");
        }

        if (ClaimsUserContextProvider.IsDaemon(User.Identity as ClaimsIdentity, out string? daemonClientId))
        {
            if (roles is null)
            {
                throw new Exception("App cannot call user-only action."); // , HttpStatusCode.Forbidden, "UserOnlyAction"
            }

            if (IsAlmightyDaemon(daemonClientId) || !(roles.Length > 0)) // SkipPermissionCheck || 
            {
                return (true, roles);
            }

            HttpContext.ValidateAppRole(roles);

            string[] permissions = HttpContext.User
                .FindAll(static c => c.Type is ClaimConstants.Role or ClaimConstants.Roles)
                .SelectMany(static c => c.Value.Split(' '))
                .Intersect(roles)
                .ToArray();

            return (true, permissions);
        }
        else
        {
            if (scopes is null)
            {
                throw new Exception("User cannot call app-only action."); // , HttpStatusCode.Forbidden, "AppOnlyAction"
            }

            if (!(scopes.Length > 0)) // SkipPermissionCheck || 
            {
                return (false, scopes);
            }

            HttpContext.VerifyUserHasAnyAcceptedScope(scopes);

            string[] permissions = HttpContext.User
                .FindFirst(static c => c.Type is ClaimConstants.Scp or ClaimConstants.Scope)!
                .Value
                .Split(' ')
                .Intersect(scopes)
                .ToArray();

            return (false, permissions);
        }
    }
    protected bool IsAlmightyDaemon(string? daemonClientId)
    {
        return isNonProduction;
        //  &&
        //     !string.IsNullOrEmpty(daemonClientId) && string.Equals(daemonClientId, systemContext.Options.AlmightyDaemonClientId, StringComparison.OrdinalIgnoreCase)
    }
}
