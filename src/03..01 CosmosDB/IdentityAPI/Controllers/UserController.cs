using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Claims;

namespace IdentityAPI.Controllers;

public interface IUserContextProvider
{
    //Task<UserContext> GetUserContextAsync();
}

public class ClaimsUserContextProvider
    : IUserContextProvider
{
    private readonly ILogger logger;
    protected readonly IHttpContextAccessor httpContextAccessor;

    /// <summary>
    /// User context provider that extracts user context from claims.
    /// </summary>
    public ClaimsUserContextProvider(ILogger<ClaimsUserContextProvider> logger, IHttpContextAccessor httpContextAccessor)
        : this((ILogger)logger, httpContextAccessor) { }

    protected ClaimsUserContextProvider(ILogger logger, IHttpContextAccessor httpContextAccessor)
    {
        this.logger = logger;
        this.httpContextAccessor = httpContextAccessor;
    }

    protected virtual bool IsAvailable([NotNullWhen(true)] ClaimsPrincipal? principal) => principal is { Identity.IsAuthenticated: true };

    //public Task<UserContext> GetUserContextAsync()
    //{
    //    using var activity = Observability.ActivitySource.StartMethodActivity(logger);

    //    ClaimsPrincipal? principal = httpContextAccessor.HttpContext?.User;
    //    UserContext userContext = IsAvailable(principal) ? CreateUserContext(principal) : new UserContext();

    //    activity?.SetOutput(userContext);
    //    return Task.FromResult(userContext);
    //}

    //protected virtual UserContext CreateUserContext(ClaimsPrincipal principal)
    //{
    //    if (principal.Identity is not ClaimsIdentity identity ||
    //        !Guid.TryParse(identity.FindFirst(ClaimConstants.ObjectId)?.Value, out Guid userId))
    //    {
    //        throw new NhException("Principal not available due to missing id claim", HttpStatusCode.Unauthorized, "MissingIdClaim");
    //    }

    //    string? email;
    //    if (IsDaemon(identity, out _))
    //    {
    //        email = null;
    //    }
    //    else
    //    {
    //        email = (identity.FindFirst(ClaimTypes.Upn) ?? identity.FindFirst(ClaimTypes.Email))?.Value;
    //        if (string.IsNullOrEmpty(email))
    //        {
    //            throw new NhException("User not available due to missing email claim", HttpStatusCode.Unauthorized, "MissingEmailClaim");
    //        }
    //    }

    //    return new UserContext() { UserId = userId, Email = email };
    //}

    public static bool IsDaemon(ClaimsIdentity? identity, [NotNullWhen(true)] out string? daemonClientId)
    {
        if (identity is not null &&
            (identity.FindFirst(ClaimConstants.Oid) ?? identity.FindFirst(ClaimConstants.ObjectId))?.Value is { } oid &&
            (identity.FindFirst(ClaimConstants.Sub) ?? identity.FindFirst(ClaimConstants.NameIdentifierId))?.Value is { } sub &&
            oid == sub)
        {
            daemonClientId = identity.FindFirst("appid")!.Value;
            return true;
        }
        else
        {
            daemonClientId = null;
            return false;
        }
    }
}


[Authorize]
[Route("[controller]")]
[ApiController]
//[RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
public class UserController : ControllerBase
{
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
    //[RequiredScope("access_as_app")]
    public async Task<IEnumerable<User>> GetUsers()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        CheckPermissions(["Users.ReadAll"], ["access_as_app"]);

        var container = identityCosmosClient.GetContainer(identityCosmosDBOptions.Database, identityCosmosDBOptions.Collection); 

        var type = "User";
        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.Type = @type")
                                                 .WithParameter("@type", type);

        var iterator = container.GetItemQueryIterator<User>(queryDefinition);
        var result = await iterator.GetItemsAsync();

        activity?.SetOutput(result);
        return result;
    }

    [HttpGet("userprofiles")]
    public async Task<IEnumerable<User>> GetUserProfiles()
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger);

        var container = identityCosmosClient.GetContainer(identityCosmosDBOptions.Database, identityCosmosDBOptions.Collection);

        var type = "User";
        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.Type = @type")
                                                 .WithParameter("@type", type);

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
