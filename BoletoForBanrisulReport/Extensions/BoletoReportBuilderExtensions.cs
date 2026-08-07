using BoletoForBanrisul.Report.Utils;
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
        /// Registra o <see cref="BoletoReportBuilder"/> como transiente e, como singleton, as duas
        /// dependências caras que ele consome: o <see cref="IPdfConverter"/> (processo Chromium) e o
        /// <see cref="IBoletoTemplateRenderer"/> (engine Razor e cache de templates).
        /// </summary>
        /// <remarks>
        /// <para>
        /// O <see cref="IBoletoReport"/> não é registrado: ele carrega o
        /// <see cref="PrintSettings"/>, que pode mudar a cada boleto. Construa um por uso, com as
        /// configurações do momento:
        /// </para>
        /// <code>
        /// using var report = builder
        ///     .Configure(options => options.Beneficiary = beneficiarioDoRequest)
        ///     .Build();
        /// </code>
        /// <para>
        /// Todos os registros usam <c>TryAdd</c> e nada é sobrescrito: um serviço já registrado
        /// permanece, e uma escolha feita em <paramref name="configureBuilder"/> - por
        /// <see cref="BoletoReportBuilder.SetPdfConverter"/>,
        /// <see cref="BoletoReportBuilder.ConfigureChromium"/> ou
        /// <see cref="BoletoReportBuilder.SetTemplateRenderer"/> - também é respeitada.
        /// </para>
        /// <para>
        /// O contêiner descarta, no fim da vida do provider, os serviços <see cref="IDisposable"/>
        /// que ele mesmo criou.
        /// </para>
        /// </remarks>
        /// <param name="services">Coleção de serviços onde o gerador será registrado.</param>
        /// <param name="configureBuilder">
        /// Ação opcional para configurar o BoletoReportBuilder, executada a cada resolução. Recebe o
        /// <see cref="IServiceProvider"/> do container, permitindo resolver outros serviços
        /// registrados.
        /// </param>
        /// <param name="configureChromium">
        /// Ação opcional para configurar o conversor Chromium singleton - timeouts, espaço mínimo em
        /// disco, argumentos, logger e concorrência. Ignorada quando já existe um
        /// <see cref="IPdfConverter"/> registrado.
        /// </param>
        public static IServiceCollection AddBoletoForBanrisulReportBuilder(
            this IServiceCollection services,
            Action<BoletoReportBuilder, IServiceProvider>? configureBuilder = null,
            Action<ChromiumConverterSettings>? configureChromium = null)
        {
            services.TryAddSingleton<IPdfConverter>(_ => configureChromium != null
                ? new ChromiumHtmlToPdfConverter(configureChromium)
                : new ChromiumHtmlToPdfConverter());

            services.TryAddSingleton<IBoletoTemplateRenderer>(_ => RazorBoletoTemplateRenderer.Default);

            services.TryAddTransient(provider =>
            {
                var builder = new BoletoReportBuilder();

                configureBuilder?.Invoke(builder, provider);

                // Depois da configuração, e só onde ela não escolheu nada.
                if (!builder.HasPdfConverterConfigured)
                    builder.SetPdfConverter(provider.GetRequiredService<IPdfConverter>());

                if (!builder.HasTemplateRendererConfigured)
                    builder.SetTemplateRenderer(provider.GetRequiredService<IBoletoTemplateRenderer>());

                return builder;
            });

            return services;
        }
    }
}
