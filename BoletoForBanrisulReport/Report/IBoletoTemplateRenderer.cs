using BoletoForBanrisul.Report.ViewModels;
using System.Threading;

namespace BoletoForBanrisul.Report
{
    /// <summary>
    /// Renderiza o HTML do boleto a partir do modelo de impressão.
    /// </summary>
    /// <remarks>
    /// É separado do <see cref="IBoletoReport"/> porque tem outro tempo de vida: o template é
    /// configuração de aplicação e o renderizador é caro de montar, enquanto o
    /// <see cref="PrintSettings"/> pode mudar a cada boleto.
    /// </remarks>
    public interface IBoletoTemplateRenderer
    {
        /// <summary>
        /// Renderiza o HTML do boleto.
        /// </summary>
        /// <param name="viewModel">O modelo com os dados já preparados para o template.</param>
        /// <param name="cancellationToken">Token para abortar a renderização.</param>
        /// <returns>O HTML do boleto.</returns>
        Task<string> RenderAsync(PrintViewModel viewModel, CancellationToken cancellationToken = default);
    }
}
