using Diginsight.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactApp.Api.Models;

namespace ReactApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientConfigController : ControllerBase
    {
        private readonly ILogger<ClientConfigController> logger;
        private readonly IConfiguration configuration;

        public ClientConfigController(ILogger<ClientConfigController> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        /// <summary>
        /// Returns the authentication configuration (client id / authority / scopes) that the
        /// React SPA uses to bootstrap MSAL. This endpoint is anonymous so the client can retrieve
        /// it before the user signs in, and it lets the client avoid storing its own app registration id.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("auth")]
        public IActionResult GetAuthConfig()
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger);

            var payload = new ClientAuthConfigResponse
            {
                ClientId = configuration["SpaClientAuth:ClientId"],
                Authority = configuration["SpaClientAuth:Authority"],
                ValidateAuthority = bool.TryParse(configuration["SpaClientAuth:ValidateAuthority"], out var validateAuthority)
                    ? validateAuthority
                    : null,
                Scopes = configuration["SpaClientAuth:Scopes"],
            };

            return Ok(payload);
        }
    }
}
