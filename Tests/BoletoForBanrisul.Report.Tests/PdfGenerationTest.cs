using BoletoForBanrisul.Core.Models;
using System.IO.Compression;

namespace BoletoForBanrisul.Report.Tests
{
    /// <summary>
    /// The half of the old <c>Test/Integration/PdfGenerationTest</c> that never needed headless
    /// Chrome/Chromium: zip file-name templating driven through a stubbed <see cref="Utils.IPdfConverter"/>.
    /// The real, Chrome-backed PDF generation stayed in <c>IntegrationTests/PdfGenerationTest</c>.
    /// </summary>
    [Trait("Category", "Unit")]
    public class PdfGenerationTest
    {
        [Theory]
        [InlineData("{NossoNumero}_{SeuNumero}_{DataVencimento}_{NomePagador}", "123456_7890_20241005_Clie_nte Teste.pdf")]
        [InlineData("{NossoNumero}_{DataEmissao}_{CpfCnpjPagador}", "123456_20240930_12345678900.pdf")]
        [InlineData("{SeuNumero}_{NomePagador}_{DataVencimento}", "7890_Clie_nte Teste_20241005.pdf")]
        [InlineData("{NomeBeneficiario}_{CpfCnpjBeneficiario}_{NossoNumero}", "Beneficiario Teste_23456789000000_123456.pdf")]
        public async Task Should_Generate_Zip_File_Names_Successfully(string fileNameTemplate, string expectedFileName)
        {
            // Arrange.
            var printSettings = new PrintSettings
            {
                FileNameTemplate = fileNameTemplate,
                Beneficiary = new BeneficiarySettings
                {
                    Name = "Beneficiario Teste",
                    CpfCnpj = "23456789000000"
                }
            };

            var pdfConverter = new RecordingPdfConverter();

            var boletoReport = new BoletoReport(printSettings, pdfConverter);

            var boletos = new List<Boleto>
            {
                new Boleto
                {
                    Titulo = new Titulo
                    {
                        NossoNumero = "123456",
                        SeuNumero = "7890",
                        DataVencimento = new DateTime(2024, 10, 5),
                        DataEmissao = new DateTime(2024, 9, 30),
                        Pagador = new Pagador { CpfCnpj = "12345678900", Nome = "Clie/nte Teste" }
                    }
                }
            };

            // Act.
            var zipBytes = await boletoReport.ZipBoletosAsync(boletos);

            using var zipStream = new MemoryStream(zipBytes);
            zipStream.Seek(0, SeekOrigin.Begin);

            using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            var entry = zipArchive.Entries.FirstOrDefault();

            // Assert.
            Assert.NotNull(entry);
            Assert.Equal(expectedFileName, entry.Name);
        }

        [Fact]
        public async Task Should_Preserve_Alphanumeric_Cnpj_Characters_In_Zip_File_Names()
        {
            var printSettings = new PrintSettings
            {
                FileNameTemplate = "{CpfCnpjPagador}_{CpfCnpjBeneficiario}",
                Beneficiary = new BeneficiarySettings
                {
                    CpfCnpj = "98.XYZ.765/43AB-10"
                }
            };

            var boletoReport = new BoletoReport(printSettings, new RecordingPdfConverter());
            var boleto = new Boleto
            {
                Titulo = new Titulo
                {
                    Pagador = new Pagador { CpfCnpj = "12.ABC.345/01DE-35" }
                }
            };

            var zipBytes = await boletoReport.ZipBoletosAsync(new[] { boleto });

            using var zipStream = new MemoryStream(zipBytes);
            using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            var entry = Assert.Single(zipArchive.Entries);

            Assert.Equal("12ABC34501DE35_98XYZ76543AB10.pdf", entry.Name);
        }
    }
}
