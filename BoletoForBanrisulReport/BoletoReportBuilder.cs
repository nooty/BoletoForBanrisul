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
    /// Builder para IBoletoReport.
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
        /// Configura as definições de impressão utilizando uma ação fornecida.
        /// </summary>
        /// <param name="handleSettings">A ação que define as configurações de impressão.</param>
        /// <returns></returns>
        public BoletoReportBuilder Configure(Action<PrintSettings> handleSettings)
        {
            handleSettings(_settings);

            return this;
        }

        /// <summary>
        /// Define o conversor de PDF a ser utilizado pelo builder.
        /// Esta configuração é opcional. Caso não seja definida, será utilizado o ChromiumHtmlToPdfConverter por padrão.
        /// </summary>
        /// <param name="pdfConverter">Instância do conversor de PDF a ser usada.</param>
        /// <returns></returns>
        public BoletoReportBuilder SetPdfConverter(IPdfConverter pdfConverter)
        {
            _pdfConverter = pdfConverter ?? throw new ArgumentNullException(nameof(pdfConverter));

            return this;
        }

        /// <summary>
        /// Configura o conversor Chromium padrão - timeouts, espaço mínimo em disco, argumentos,
        /// logger e concorrência.
        /// Esta configuração é opcional e não pode ser combinada com <see cref="SetPdfConverter"/>.
        /// </summary>
        /// <param name="configureChromium">A ação que ajusta as configurações do conversor.</param>
        /// <returns></returns>
        public BoletoReportBuilder ConfigureChromium(Action<ChromiumConverterSettings> configureChromium)
        {
            _configureChromium = configureChromium ?? throw new ArgumentNullException(nameof(configureChromium));

            return this;
        }

        /// <summary>
        /// Indica se o conversor de PDF já foi decidido, por <see cref="SetPdfConverter"/> ou por
        /// <see cref="ConfigureChromium"/>.
        /// </summary>
        /// <remarks>
        /// O registro de DI injeta o conversor do contêiner só quando isto é <c>false</c>.
        /// </remarks>
        internal bool HasPdfConverterConfigured => _pdfConverter != null || _configureChromium != null;

        /// <summary>
        /// Define o renderizador do HTML do boleto, para usar um template próprio ou compartilhar um
        /// renderizador já construído.
        /// Esta configuração é opcional. Caso não seja definida, é usado
        /// <see cref="RazorBoletoTemplateRenderer.Default"/>, com o template embutido no pacote.
        /// </summary>
        /// <remarks>
        /// O renderizador é caro de construir e seguro de compartilhar: passe sempre a mesma
        /// instância.
        /// </remarks>
        /// <param name="templateRenderer">Instância do renderizador a ser usada.</param>
        /// <returns></returns>
        public BoletoReportBuilder SetTemplateRenderer(IBoletoTemplateRenderer templateRenderer)
        {
            _templateRenderer = templateRenderer ?? throw new ArgumentNullException(nameof(templateRenderer));

            return this;
        }

        /// <summary>
        /// Indica se o renderizador já foi definido por <see cref="SetTemplateRenderer"/>.
        /// </summary>
        internal bool HasTemplateRendererConfigured => _templateRenderer != null;

        /// <summary>
        /// Constrói IBoletoReport com as configurações fornecidas.
        /// </summary>
        /// <remarks>
        /// Sem <see cref="SetPdfConverter"/>, o relatório construído é dono do conversor Chromium
        /// criado aqui e o descarta no próprio <see cref="IDisposable.Dispose"/>. Para chamar
        /// <see cref="Build"/> repetidamente, forneça um conversor compartilhado, para não subir um
        /// Chromium por relatório.
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
