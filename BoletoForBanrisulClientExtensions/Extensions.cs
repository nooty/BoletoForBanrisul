using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.Extensions
{
    /// <summary>
    /// Métodos de extensão para registrar o BoletoClientBuilder no contêiner de injeção de dependência do .NET.
    /// </summary>
    public static class BoletoClientBuilderExtensions
    {
        /// <summary>
        /// Adiciona o BoletoClientBuilder ao contêiner de injeção de dependência como um serviço transiente.
        /// Permite que o usuário configure o builder opcionalmente por meio de uma ação.
        /// </summary>
        /// <param name="services">Coleção de serviços onde o BoletoClientBuilder será registrado.</param>
        /// <param name="configureBuilder">Ação opcional para configurar o BoletoClientBuilder.</param>
        public static IServiceCollection AddBoletoForBanrisulClientBuilder(this IServiceCollection services, Action<BoletoClientBuilder>? configureBuilder = null)
        {
            services.TryAddTransient(provider =>
            {
                var builder = new BoletoClientBuilder();

                configureBuilder?.Invoke(builder);

                return builder;
            });

            return services;
        }
    }
}
