using BoletoForBanrisul.Report.Utils;

namespace BoletoForBanrisul.Report.Tests
{
    /// <summary>
    /// Ownership: the report disposes the converter it created, and never one it was handed. Getting
    /// that backwards either leaks a Chromium per report or kills a shared converter the first time
    /// any report is disposed.
    /// </summary>
    [Trait("Category", "Unit")]
    public class BoletoReportBuilderTests
    {
        [Fact]
        public void Dispose_DoesNotDisposeAnInjectedConverter()
        {
            var injected = new RecordingPdfConverter();

            var report = new BoletoReportBuilder()
                .SetPdfConverter(injected)
                .Build();

            report.Dispose();

            Assert.False(injected.WasDisposed);
        }

        [Fact]
        public void Dispose_DisposesAnOwnedConverter()
        {
            // The owning constructor is internal because ownership is a decision Build() makes, not
            // something a caller can claim.
            var owned = new RecordingPdfConverter();

            var report = new BoletoReport(new PrintSettings(), owned, ownsPdfConverter: true);

            report.Dispose();

            Assert.True(owned.WasDisposed);
        }

        [Fact]
        public void Dispose_DoesNotDisposeAConverterPassedToTheConstructor()
        {
            var injected = new RecordingPdfConverter();

            var report = new BoletoReport(new PrintSettings(), injected);

            report.Dispose();

            Assert.False(injected.WasDisposed);
        }

        [Fact]
        public void Build_WithBothSetPdfConverterAndConfigureChromium_Throws()
        {
            var builder = new BoletoReportBuilder()
                .SetPdfConverter(new RecordingPdfConverter())
                .ConfigureChromium(settings => settings.MinimumFreeDiskMegabytes = 0);

            var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

            Assert.Contains("ConfigureChromium", exception.Message);
        }

        [Fact]
        public void ConfigureChromium_AppliesTheSettingsToTheDefaultConverter()
        {
            // The invalid value only reaches validation if ConfigureChromium is actually applied to
            // the converter Build() creates.
            var builder = new BoletoReportBuilder()
                .ConfigureChromium(settings => settings.MaxConcurrentConversions = 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());
        }

        [Fact]
        public void Build_WithoutAConverter_ProducesADisposableReport()
        {
            using var report = new BoletoReportBuilder()
                .ConfigureChromium(settings => settings.MinimumFreeDiskMegabytes = 0)
                .Build();

            Assert.IsAssignableFrom<IDisposable>(report);
        }

        [Fact]
        public void SetPdfConverter_WithNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new BoletoReportBuilder().SetPdfConverter(null!));
        }

        [Fact]
        public void ConfigureChromium_WithNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new BoletoReportBuilder().ConfigureChromium(null!));
        }
    }
}
