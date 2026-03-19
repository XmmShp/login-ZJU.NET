using Microsoft.Extensions.DependencyInjection;

namespace LoginZju;

/// <summary>
/// Extension methods for registering login-ZJU.NET factories with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ILoginZjuFactory"/> for creating per-user ZJUAM auth and service instances.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The <see cref="IServiceCollection"/> for further chaining.</returns>
    public static IServiceCollection AddLoginZju(this IServiceCollection services)
    {
        services.AddSingleton<ILoginZjuFactory, LoginZjuFactory>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="ICc98ServiceFactory"/> for creating per-user CC98 service instances.
    /// CC98 uses a separate account system, not ZJUAM. The app-level OAuth2 client credentials
    /// (ClientId, ClientSecret) are configured here; per-user credentials are provided at creation time.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure <see cref="Cc98Options"/> (ClientId, ClientSecret, TokenUrl).</param>
    /// <returns>The <see cref="IServiceCollection"/> for further chaining.</returns>
    public static IServiceCollection AddLoginZjuCc98(this IServiceCollection services, Action<Cc98Options> configure)
    {
        services.Configure(configure);
        services.AddSingleton<ICc98ServiceFactory, Cc98ServiceFactory>();
        return services;
    }
}
