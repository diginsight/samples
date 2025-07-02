using Microsoft.Identity.Web;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace IdentityAPI.Controllers;

public class ClaimsUserContextProvider
    : IUserContextProvider
{
    private static readonly Type T = typeof(ClaimsUserContextProvider);
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
