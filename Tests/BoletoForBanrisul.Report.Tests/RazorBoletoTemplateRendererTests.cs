using BoletoForBanrisul.Report.ViewModels;

namespace BoletoForBanrisul.Report.Tests
{
    /// <summary>
    /// Building a renderer resolves Roslyn metadata references and starts an empty template cache, so
    /// one per <see cref="BoletoReport"/> would recompile the boleto template on every report. Hence
    /// the split: the template is application configuration, while <see cref="PrintSettings"/> can
    /// change per boleto.
    /// </summary>
    [Trait("Category", "Unit")]
    public class RazorBoletoTemplateRendererTests
    {
        [Fact]
        public void Default_IsASingleSharedInstance()
        {
            Assert.Same(RazorBoletoTemplateRenderer.Default, RazorBoletoTemplateRenderer.Default);
        }

        [Fact]
        public void Reports_BuiltWithoutARenderer_ShareTheDefaultOne()
        {
            using var first = new BoletoReport(new PrintSettings(), new RecordingPdfConverter());
            using var second = new BoletoReport(new PrintSettings(), new RecordingPdfConverter());

            Assert.NotSame(first, second);
            Assert.Same(first.TemplateRenderer, second.TemplateRenderer);
            Assert.Same(RazorBoletoTemplateRenderer.Default, first.TemplateRenderer);
        }

        [Fact]
        public void Build_UsesTheRendererGivenToTheBuilder()
        {
            var renderer = new RecordingTemplateRenderer();

            using var report = (BoletoReport)new BoletoReportBuilder()
                .SetPdfConverter(new RecordingPdfConverter())
                .SetTemplateRenderer(renderer)
                .Build();

            Assert.Same(renderer, report.TemplateRenderer);
        }

        [Fact]
        public async Task PrintAsync_RendersThroughTheConfiguredRenderer()
        {
            var renderer = new RecordingTemplateRenderer();
            var converter = new RecordingPdfConverter();

            using var report = new BoletoReport(new PrintSettings(), converter, renderer);

            await report.PrintAsync(CreateBoleto());

            Assert.NotNull(renderer.LastViewModel);
            Assert.Equal(RecordingTemplateRenderer.Html, converter.LastHtml);
        }

        [Fact]
        public void Constructor_WithAMissingTemplateDirectory_Throws()
        {
            var missing = Path.Combine(Path.GetTempPath(), $"nao-existe-{Guid.NewGuid():N}");

            Assert.Throws<InvalidOperationException>(() => new RazorBoletoTemplateRenderer(missing, "Boleto.cshtml"));
        }

        [Fact]
        public void Constructor_WithAMissingTemplateFile_Throws()
        {
            var directory = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), $"boleto-templates-{Guid.NewGuid():N}"));

            try
            {
                // The renderer is the only thing that knows where templates come from, so it is where
                // the template source is validated.
                Assert.Throws<InvalidOperationException>(
                    () => new RazorBoletoTemplateRenderer(directory.FullName, "Boleto.cshtml"));
            }
            finally
            {
                directory.Delete(recursive: true);
            }
        }

        [Fact]
        public void Constructor_WithEmptyTemplateArguments_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RazorBoletoTemplateRenderer("", "Boleto.cshtml"));
            Assert.Throws<ArgumentException>(() => new RazorBoletoTemplateRenderer(Path.GetTempPath(), ""));
        }

        private static Core.Models.Boleto CreateBoleto() => new Core.Models.Boleto
        {
            Titulo = new Core.Models.Titulo
            {
                NossoNumero = "123456",
                SeuNumero = "7890",
                DataVencimento = new DateTime(2026, 10, 5),
                DataEmissao = new DateTime(2026, 9, 30),
                Pagador = new Core.Models.Pagador { CpfCnpj = "12345678900", Nome = "PAGADOR TESTE" }
            }
        };

        private sealed class RecordingTemplateRenderer : IBoletoTemplateRenderer
        {
            internal const string Html = "<html>rendered</html>";

            internal PrintViewModel? LastViewModel { get; private set; }

            public Task<string> RenderAsync(PrintViewModel viewModel, CancellationToken cancellationToken = default)
            {
                LastViewModel = viewModel;

                return Task.FromResult(Html);
            }
        }
    }
}
