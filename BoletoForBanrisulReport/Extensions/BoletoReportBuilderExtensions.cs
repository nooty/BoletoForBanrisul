using BoletoForBanrisul.Report.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace BoletoForBanrisul.Report.Extensions
{
    /// <summary>
    /// Extension methods for registering the BoletoReportBuilder in .NET's dependency injection container.
    /// </summary>
    public static class BoletoReportBuilderExtensions
    {
        /// <summary>
        /// Registers the <see cref="BoletoReportBuilder"/> as transient and, as singletons, the two
        /// expensive dependencies it consumes: the <see cref="IPdfConverter"/> (Chromium process) and the
        /// <see cref="IBoletoTemplateRenderer"/> (Razor engine and template cache).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <see cref="IBoletoReport"/> is not registered: it carries the <see cref="PrintSettings"/>,
        /// which can change per boleto. Build one per use, with the settings of the moment:
        /// </para>
        /// <code>
        /// using var report = builder
        ///     .Configure(options => options.Beneficiary = beneficiarioDoRequest)
        ///     .Build();
        /// </code>
        /// <para>
        /// Every registration uses <c>TryAdd</c> and nothing is overwritten: an already registered
        /// service stays, and a choice made in <paramref name="configureBuilder"/> - through
        /// <see cref="BoletoReportBuilder.SetPdfConverter"/>,
        /// <see cref="BoletoReportBuilder.ConfigureChromium"/> or
        /// <see cref="BoletoReportBuilder.SetTemplateRenderer"/> - is respected too.
        /// </para>
        /// <para>
        /// The container disposes, at the end of the provider's life, the <see cref="IDisposable"/>
        /// services it created itself.
        /// </para>
        /// </remarks>
        /// <param name="services">Service collection the generator is registered in.</param>
        /// <param name="configureBuilder">
        /// Optional action that configures the BoletoReportBuilder, run on every resolution. It receives
        /// the container's <see cref="IServiceProvider"/>, so other registered services can be resolved.
        /// </param>
        /// <param name="configureChromium">
        /// Optional action that configures the singleton Chromium converter - timeouts, minimum free disk
        /// space, arguments, logger and concurrency. Ignored when an <see cref="IPdfConverter"/> is
        /// already registered.
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

                // After configuration, and only where it chose nothing.
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
