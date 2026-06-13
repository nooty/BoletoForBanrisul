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
            _pdfConverter = pdfConverter;

            return this;
        }

        /// <summary>
        /// Constrói IBoletoReport com as configurações fornecidas.
        /// </summary>
        /// <returns></returns>
        public IBoletoReport Build()
        {
            var converter = _pdfConverter ?? new ChromiumHtmlToPdfConverter();

            return new BoletoReport(_settings, converter);
        }
    }
}
