using BoletoForBanrisul.Core.Models;
using System;
using System.Threading;

namespace BoletoForBanrisul.Report
{
    /// <summary>
    /// Generates boleto PDFs.
    /// </summary>
    /// <remarks>
    /// It is <see cref="IDisposable"/> because the converter created by <see cref="BoletoReportBuilder"/>
    /// carries process resources (a semaphore and, indirectly, Chromium). A converter supplied through
    /// <see cref="BoletoReportBuilder.SetPdfConverter"/> remains the caller's and is not disposed here.
    /// </remarks>
    public interface IBoletoReport : IDisposable
    {
        /// <summary>
        /// Generates a ZIP file holding boletos in PDF format.
        /// </summary>
        /// <param name="boletos">The boletos to convert to PDF and include in the ZIP file.</param>
        /// <param name="cancellationToken">Token that aborts the generation.</param>
        /// <returns>A byte array holding the generated ZIP file.</returns>
        Task<byte[]> ZipBoletosAsync(IEnumerable<Boleto> boletos, CancellationToken cancellationToken = default);
        /// <summary>
        /// Generates the PDF of a single boleto.
        /// </summary>
        /// <param name="boleto">The boleto to convert to PDF.</param>
        /// <param name="cancellationToken">Token that aborts the generation.</param>
        /// <returns>A byte array holding the boleto's generated PDF.</returns>
        Task<byte[]> PrintAsync(Boleto boleto, CancellationToken cancellationToken = default);
    }
}
