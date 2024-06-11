using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using System.Text;

namespace SampleWebApi;

public static class ConfigureRedisCacheExtension
{
    public static IServiceCollection ConfigureRedisCacheSettings(this IServiceCollection services, IConfiguration configuration)
    {

        services.Configure<RedisCacheOptions>("RedisCacheConfig", opt =>
        {
            opt.Connectionstring = configuration.GetValue<string>("RedisLockConnectionString");
        });

        return services;
    }
}

