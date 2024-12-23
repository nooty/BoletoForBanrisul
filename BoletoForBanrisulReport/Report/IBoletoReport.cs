using BoletoForBanrisul.Core.Models;

namespace BoletoForBanrisul.Report
{
    public interface IBoletoReport
    {
        /// <summary>
        /// Gera um arquivo ZIP contendo boletos em formato PDF.
        /// </summary>
        /// <param name="boletos">A coleção de boletos a serem convertidos para PDF e incluídos no arquivo ZIP.</param>
        /// <returns>Um array de bytes representando o arquivo ZIP gerado.</returns>
        Task<byte[]> ZipBoletosAsync(IEnumerable<Boleto> boletos);
        /// <summary>
        /// Gera o PDF de um único boleto.
        /// </summary>
        /// <param name="boleto">O boleto a ser convertido para PDF.</param>
        /// <returns>Um array de bytes representando o PDF gerado do boleto.</returns>
        Task<byte[]> PrintAsync(Boleto boleto);
    }
}