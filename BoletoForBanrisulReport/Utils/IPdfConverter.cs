using System;
using System.Collections.Generic;
using System.Text;

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
        /// <returns>O arquivo PDF em formato de array de bytes.</returns>
        Task<byte[]> ConvertToPdfAsync(string html);
    }
}
