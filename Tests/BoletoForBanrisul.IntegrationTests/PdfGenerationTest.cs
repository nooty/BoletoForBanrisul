using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using BoletoForBanrisul.Report;

namespace BoletoForBanrisul.IntegrationTests
{
    /// <summary>
    /// Real, Chrome/Chromium-backed PDF generation. The zip file-name templating half of the old
    /// <c>Test/Integration/PdfGenerationTest</c>, which only ever needed a stubbed
    /// <see cref="Report.Utils.IPdfConverter"/> and never touched a browser, now lives ungated in
    /// <c>Report.Tests/PdfGenerationTest</c>.
    /// </summary>
    [Trait("Category", "Integration")]
    public class PdfGenerationTest
    {
        [IntegrationFact]
        public async Task Should_Generate_PDF_From_Boleto1_Object()
        {
            Boleto boleto = BoletoGenerator.GenerateBoleto1();

            boleto.Titulo.Mensagens = null;
            boleto.Titulo.Instrucoes = new Instrucoes()
            {
                Baixa = new Baixa()
                {
                    Codigo = CodigoBaixa.BaixarDevolver,
                    Prazo = 99
                },
            };

            var report = new BoletoReportBuilder()
				.Configure((PrintSettings options) =>
                {
                    options.PaymentLocation = "PAGÁVEL EM QUALQUER BANCO ATÉ O VENCIMENTO.";
                    options.Beneficiary = new BeneficiarySettings {
                        Code = "0120998844513",
                        Name = "EMPRESA TESTE",
                        CpfCnpj = "12345678901123",
                        Address = "RUA TESTE, 123",
                        City = "PORTO ALEGRE",
                        State = "RS",
                        ZipCode = "90000-000"
                    };
                })
				.Build();

			var pdf = await report.PrintAsync(boleto);

            // Salva o arquivo.
            File.WriteAllBytes("boleto1-test.pdf", pdf);

            Assert.NotNull(pdf);
        }

        [IntegrationFact]
        public async Task Should_Generate_Distinct_PDFs_For_Different_Boletos()
        {
            int numberOfBoletos = 100;

            List<Boleto> boletos = Enumerable.Range(1, numberOfBoletos)
                                             .Select(i => {
                                                var boleto = BoletoGenerator.GenerateBoleto1();
                                                 boleto.Titulo.Pagador.Nome += $" - {i}";
                                                 boleto.Titulo.ValorNominal = i;
                                                return boleto;
                                             })
                                             .ToList();

            var pdfTasks = boletos.Select(async boleto =>
            {
                var report = new BoletoReportBuilder()
                    .Configure((PrintSettings options) =>
                    {
                        options.PaymentLocation = "PAGÁVEL EM QUALQUER BANCO ATÉ O VENCIMENTO.";
                        options.Beneficiary = new BeneficiarySettings
                        {
                            Code = "123456",
                            Name = "EMPRESA TESTE",
                            CpfCnpj = "12345678901123",
                            Address = "RUA TESTE, 123",
                            City = "PORTO ALEGRE",
                            State = "RS",
                            ZipCode = "90000-000"
                        };
                    })
                    .Build();

                return await report.PrintAsync(boleto);
            });

            byte[][] pdfResults = await Task.WhenAll(pdfTasks);

            for (int i = 0; i < pdfResults.Length; i++)
            {
                for (int j = i + 1; j < pdfResults.Length; j++)
                {
                    Assert.NotEqual(pdfResults[i], pdfResults[j]);
                }
            }

            // Persiste os PDFs gerados para inspeção manual.
            for (int i = 0; i < pdfResults.Length; i++)
            {
                File.WriteAllBytes($"boleto_test_{i + 1}.pdf", pdfResults[i]);
            }

            Assert.All(pdfResults, pdf => Assert.NotNull(pdf));
        }
    }
}
