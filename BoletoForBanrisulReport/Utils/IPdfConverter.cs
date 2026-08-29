using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BoletoForBanrisul.Report.Utils
{
    /// <summary>
    /// Converts HTML to PDF.
    /// </summary>
    public interface IPdfConverter
    {
        /// <summary>
        /// Asynchronously converts an HTML string into a PDF document.
        /// </summary>
        /// <param name="html">The HTML content to convert to PDF.</param>
        /// <param name="cancellationToken">
        /// Token that aborts the conversion. Implementations must honour it - including while waiting
        /// for another conversion in progress - and throw <see cref="OperationCanceledException"/> when
        /// cancelled.
        /// </param>
        /// <returns>The PDF file as a byte array.</returns>
        Task<byte[]> ConvertToPdfAsync(string html, CancellationToken cancellationToken = default);
    }
}
