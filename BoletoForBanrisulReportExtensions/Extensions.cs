using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BoletoForBanrisul.Report.Utils;

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
        /// <param name="configureBuilder">Ação opcional para configurar o BoletoReportBuilder antes de registrá-lo.</param>
        public static IServiceCollection AddBoletoForBanrisulReportBuilder(this IServiceCollection services, Action<BoletoReportBuilder>? configureBuilder = null)
        {
            services.TryAddTransient(provider =>
            {
                var builder = new BoletoReportBuilder();

                configureBuilder?.Invoke(builder);

                return builder;
            });

            return services;
        }
    }
}
