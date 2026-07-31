using BoletoForBanrisul.Report.Utils;
using System.Text;

namespace BoletoForBanrisul.Report.Tests
{
    /// <summary>
    /// A test double for <see cref="IPdfConverter"/> that records the HTML it was asked to convert instead
    /// of invoking headless Chrome/Chromium. Replaces a Moq mock of <see cref="IPdfConverter"/>.
    /// </summary>
    /// <remarks>
    /// This is the seam the whole test project relies on to stay Chrome-free: <see cref="BoletoReport"/>
    /// renders the Razor template to an HTML string internally and only then hands it to the configured
    /// <see cref="IPdfConverter"/>. Substituting this recorder makes that intermediate HTML observable
    /// without needing a real PDF conversion (or a browser) at all.
    /// </remarks>
    internal sealed class RecordingPdfConverter : IPdfConverter
    {
        /// <summary>The HTML passed to the most recent <see cref="ConvertToPdfAsync"/> call.</summary>
        internal string LastHtml { get; private set; } = "";

        /// <summary>All HTML strings passed across every call, in order (for batch/zip scenarios).</summary>
        internal List<string> AllHtml { get; } = new List<string>();

        public Task<byte[]> ConvertToPdfAsync(string html)
        {
            LastHtml = html;
            AllHtml.Add(html);

            return Task.FromResult(Encoding.UTF8.GetBytes("%PDF-fake%"));
        }
    }
}
