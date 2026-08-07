using BoletoForBanrisul.Core.Models;
using System;
using System.Threading;

namespace BoletoForBanrisul.Report
{
    /// <summary>
    /// Gerador de PDF de boletos.
    /// </summary>
    /// <remarks>
    /// É <see cref="IDisposable"/> porque o conversor criado pelo <see cref="BoletoReportBuilder"/>
    /// carrega recursos de processo (semáforo e, indiretamente, o Chromium). Um conversor fornecido
    /// via <see cref="BoletoReportBuilder.SetPdfConverter"/> continua sendo do chamador e não é
    /// descartado aqui.
    /// </remarks>
    public interface IBoletoReport : IDisposable
    {
        /// <summary>
        /// Gera um arquivo ZIP contendo boletos em formato PDF.
        /// </summary>
        /// <param name="boletos">A coleção de boletos a serem convertidos para PDF e incluídos no arquivo ZIP.</param>
        /// <param name="cancellationToken">Token para abortar a geração.</param>
        /// <returns>Um array de bytes representando o arquivo ZIP gerado.</returns>
        Task<byte[]> ZipBoletosAsync(IEnumerable<Boleto> boletos, CancellationToken cancellationToken = default);
        /// <summary>
        /// Gera o PDF de um único boleto.
        /// </summary>
        /// <param name="boleto">O boleto a ser convertido para PDF.</param>
        /// <param name="cancellationToken">Token para abortar a geração.</param>
        /// <returns>Um array de bytes representando o PDF gerado do boleto.</returns>
        Task<byte[]> PrintAsync(Boleto boleto, CancellationToken cancellationToken = default);
    }
}
