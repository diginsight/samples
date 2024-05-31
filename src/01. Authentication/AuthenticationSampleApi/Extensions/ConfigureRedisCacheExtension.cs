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

