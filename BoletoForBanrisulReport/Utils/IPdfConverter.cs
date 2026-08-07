using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BoletoForBanrisul.Report.Utils
{
    /// <summary>
    /// Interface para conversão de HTML em PDF.
    /// </summary>
    public interface IPdfConverter
    {
        /// <summary>
        /// Converte uma string HTML em um documento PDF de forma assíncrona.
        /// </summary>
        /// <param name="html">O conteúdo HTML a ser convertido em PDF.</param>
        /// <param name="cancellationToken">
        /// Token para abortar a conversão. Implementações devem honrá-lo - inclusive enquanto
        /// esperam por outra conversão em andamento - e lançar
        /// <see cref="OperationCanceledException"/> quando cancelado.
        /// </param>
        /// <returns>O arquivo PDF em formato de array de bytes.</returns>
        Task<byte[]> ConvertToPdfAsync(string html, CancellationToken cancellationToken = default);
    }
}
