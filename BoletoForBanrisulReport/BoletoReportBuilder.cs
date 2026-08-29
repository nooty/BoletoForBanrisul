using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Report.Utils;
using BoletoForBanrisul.Report.ViewModels;
using RazorLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report
{
    /// <summary>
    /// Builder for IBoletoReport.
    /// </summary>
    public class BoletoReportBuilder
    {
        private PrintSettings _settings = new PrintSettings();
        private IPdfConverter? _pdfConverter;
        private Action<ChromiumConverterSettings>? _configureChromium;
        private IBoletoTemplateRenderer? _templateRenderer;

        public BoletoReportBuilder() {
        }

        /// <summary>
        /// Configures the print settings through the given action.
        /// </summary>
        /// <param name="handleSettings">The action that sets the print settings.</param>
        /// <returns></returns>
        public BoletoReportBuilder Configure(Action<PrintSettings> handleSettings)
        {
            handleSettings(_settings);

            return this;
        }

        /// <summary>
        /// Sets the PDF converter the builder uses.
        /// Optional. When omitted, ChromiumHtmlToPdfConverter is used by default.
        /// </summary>
        /// <param name="pdfConverter">The PDF converter instance to use.</param>
        /// <returns></returns>
        public BoletoReportBuilder SetPdfConverter(IPdfConverter pdfConverter)
        {
            _pdfConverter = pdfConverter ?? throw new ArgumentNullException(nameof(pdfConverter));

            return this;
        }

        /// <summary>
        /// Configures the default Chromium converter - timeouts, minimum free disk space, arguments,
        /// logger and concurrency.
        /// Optional, and cannot be combined with <see cref="SetPdfConverter"/>.
        /// </summary>
        /// <param name="configureChromium">The action that adjusts the converter's settings.</param>
        /// <returns></returns>
        public BoletoReportBuilder ConfigureChromium(Action<ChromiumConverterSettings> configureChromium)
        {
            _configureChromium = configureChromium ?? throw new ArgumentNullException(nameof(configureChromium));

            return this;
        }

        /// <summary>
        /// Tells whether the PDF converter has already been decided, by <see cref="SetPdfConverter"/> or
        /// by <see cref="ConfigureChromium"/>.
        /// </summary>
        /// <remarks>
        /// The DI registration injects the container's converter only when this is <c>false</c>.
        /// </remarks>
        internal bool HasPdfConverterConfigured => _pdfConverter != null || _configureChromium != null;

        /// <summary>
        /// Sets the renderer for the boleto's HTML, to use a template of your own or to share an
        /// already built renderer.
        /// Optional. When omitted, <see cref="RazorBoletoTemplateRenderer.Default"/> is used, with the
        /// template embedded in the package.
        /// </summary>
        /// <remarks>
        /// The renderer is expensive to build and safe to share: always pass the same instance.
        /// </remarks>
        /// <param name="templateRenderer">The renderer instance to use.</param>
        /// <returns></returns>
        public BoletoReportBuilder SetTemplateRenderer(IBoletoTemplateRenderer templateRenderer)
        {
            _templateRenderer = templateRenderer ?? throw new ArgumentNullException(nameof(templateRenderer));

            return this;
        }

        /// <summary>
        /// Tells whether the renderer has already been set by <see cref="SetTemplateRenderer"/>.
        /// </summary>
        internal bool HasTemplateRendererConfigured => _templateRenderer != null;

        /// <summary>
        /// Builds an IBoletoReport with the configured settings.
        /// </summary>
        /// <remarks>
        /// Without <see cref="SetPdfConverter"/>, the report built here owns the Chromium converter
        /// created for it and disposes it in its own <see cref="IDisposable.Dispose"/>. To call
        /// <see cref="Build"/> repeatedly, supply a shared converter, so as not to start one Chromium
        /// per report.
        /// </remarks>
        /// <returns></returns>
        public IBoletoReport Build()
        {
            if (_pdfConverter != null && _configureChromium != null)
                throw new InvalidOperationException("Erro no builder: 'SetPdfConverter' e 'ConfigureChromium' não podem ser usados juntos. O 'ConfigureChromium' só configura o conversor Chromium padrão, que deixa de ser usado quando um conversor próprio é fornecido.");

            var renderer = _templateRenderer ?? RazorBoletoTemplateRenderer.Default;

            if (_pdfConverter != null)
                return new BoletoReport(_settings, _pdfConverter, renderer, ownsPdfConverter: false);

            var converter = _configureChromium != null
                ? new ChromiumHtmlToPdfConverter(_configureChromium)
                : new ChromiumHtmlToPdfConverter();

            return new BoletoReport(_settings, converter, renderer, ownsPdfConverter: true);
        }
    }
}
