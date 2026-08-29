using BoletoForBanrisul.Report.ViewModels;
using System.Threading;

namespace BoletoForBanrisul.Report
{
    /// <summary>
    /// Renders the boleto's HTML from the print view model.
    /// </summary>
    /// <remarks>
    /// It is separate from <see cref="IBoletoReport"/> because it has a different lifetime: the template
    /// is application configuration and the renderer is expensive to build, while the
    /// <see cref="PrintSettings"/> can change per boleto.
    /// </remarks>
    public interface IBoletoTemplateRenderer
    {
        /// <summary>
        /// Renders the boleto's HTML.
        /// </summary>
        /// <param name="viewModel">The model carrying the data already prepared for the template.</param>
        /// <param name="cancellationToken">Token that aborts the rendering.</param>
        /// <returns>The boleto's HTML.</returns>
        Task<string> RenderAsync(PrintViewModel viewModel, CancellationToken cancellationToken = default);
    }
}
