using Microsoft.Extensions.DependencyInjection;

namespace LoginZju;

/// <summary>
/// Extension methods for registering login-ZJU.NET factories with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="ILoginZjuFactory"/> for creating per-user ZJUAM auth and service instances.
        /// </summary>
        /// <param name="configure">Action to configure <see cref="Cc98Options"/> (ClientId, ClientSecret, TokenUrl).</param>
        /// <returns>The <see cref="IServiceCollection"/> for further chaining.</returns>
        public IServiceCollection AddLoginZju(Action<Cc98Options>? configure = null)
        {
            services.AddSingleton<ILoginZjuFactory, LoginZjuFactory>();
            if (configure is not null)
            {
                services.Configure(configure);
            }
            services.AddSingleton<ICc98ServiceFactory, Cc98ServiceFactory>();
            return services;
        }
    }
}
