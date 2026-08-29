using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace BoletoForBanrisul.Client.Extensions
{
    /// <summary>
    /// Extension methods for registering the BoletoClientBuilder in .NET's dependency injection container.
    /// </summary>
    public static class BoletoClientBuilderExtensions
    {
        /// <summary>
        /// Adds the BoletoClientBuilder to the dependency injection container as a transient service.
        /// The builder can optionally be configured through an action.
        /// </summary>
        /// <param name="services">Service collection the BoletoClientBuilder is registered in.</param>
        /// <param name="configureBuilder">
        /// Optional action that configures the BoletoClientBuilder. It receives the container's
        /// <see cref="IServiceProvider"/>, so other registered services (ITokenStorage, loggers) can be
        /// resolved to configure the builder.
        /// </param>
        public static IServiceCollection AddBoletoForBanrisulClientBuilder(this IServiceCollection services, Action<BoletoClientBuilder, IServiceProvider>? configureBuilder = null)
        {
            services.TryAddTransient(provider =>
            {
                var builder = new BoletoClientBuilder();

                configureBuilder?.Invoke(builder, provider);

                return builder;
            });

            return services;
        }
    }
}
