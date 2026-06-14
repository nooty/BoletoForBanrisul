using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace BoletoForBanrisul.Report.Extensions
{
    /// <summary>
    /// Métodos de extensão para registrar o BoletoReportBuilder no contêiner de injeção de dependência do .NET.
    /// </summary>
    public static class BoletoReportBuilderExtensions
    {
        /// <summary>
        /// Adiciona o BoletoReportBuilder ao contêiner de injeção de dependência do .NET.
        /// Este método permite que o BoletoReportBuilder seja registrado como transiente no contêiner de serviços.
        /// </summary>
        /// <param name="services">Coleção de serviços onde o BoletoReportBuilder será registrado.</param>
        /// <param name="configureBuilder">
        /// Ação opcional para configurar o BoletoReportBuilder antes de registrá-lo. Recebe o
        /// <see cref="IServiceProvider"/> do container, permitindo resolver outros serviços registrados
        /// para configurar o builder.
        /// </param>
        public static IServiceCollection AddBoletoForBanrisulReportBuilder(this IServiceCollection services, Action<BoletoReportBuilder, IServiceProvider>? configureBuilder = null)
        {
            services.TryAddTransient(provider =>
            {
                var builder = new BoletoReportBuilder();

                configureBuilder?.Invoke(builder, provider);

                return builder;
            });

            return services;
        }
    }
}
