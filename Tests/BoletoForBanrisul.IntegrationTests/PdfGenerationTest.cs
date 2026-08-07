using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using BoletoForBanrisul.Report;
using BoletoForBanrisul.Report.Utils;
using System.Diagnostics;

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

            using var report = new BoletoReportBuilder()
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
            var profilesBefore = ExistingProfiles();

            int numberOfBoletos = 100;

            List<Boleto> boletos = Enumerable.Range(1, numberOfBoletos)
                                             .Select(i => {
                                                var boleto = BoletoGenerator.GenerateBoleto1();
                                                 boleto.Titulo.Pagador.Nome += $" - {i}";
                                                 boleto.Titulo.ValorNominal = i;
                                                return boleto;
                                             })
                                             .ToList();

            // Um relatório - e portanto um conversor - para as 100 conversões. Um relatório por
            // boleto seria um Chromium por boleto; o conversor já serializa internamente.
            using var report = new BoletoReportBuilder()
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

            var pdfTasks = boletos.Select(boleto => report.PrintAsync(boleto));

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

            // Cada conversão usa um profile descartável, apagado ao final. Profile que sobra aqui
            // enche o disco em produção.
            Assert.Empty(ProfilesLeakedSince(profilesBefore));
        }

        [IntegrationFact]
        public async Task Should_Abort_Conversion_When_Cancelled()
        {
            var profilesBefore = ExistingProfiles();

            using var report = new BoletoReportBuilder()
                .Configure((PrintSettings options) =>
                {
                    options.Beneficiary = new BeneficiarySettings { Name = "EMPRESA TESTE", CpfCnpj = "12345678901123" };
                })
                .Build();

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            var stopwatch = Stopwatch.StartNew();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => report.PrintAsync(BoletoGenerator.GenerateBoleto1(), cts.Token));

            stopwatch.Stop();

            // O ponto do cancelamento é não esperar o timeout inteiro da conversão.
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"O cancelamento levou {stopwatch.Elapsed}.");
            Assert.Empty(ProfilesLeakedSince(profilesBefore));
        }

        /// <summary>
        /// Profiles que apareceram depois de <paramref name="before"/> e não foram removidos.
        /// </summary>
        /// <remarks>
        /// O assert é sobre o que a conversão deixou para trás, não sobre o conteúdo da raiz: ela
        /// fica no diretório temporário da máquina e, por contrato, pode conter profiles de outros
        /// processos e de execuções anteriores.
        /// </remarks>
        private static string[] ProfilesLeakedSince(string[] before)
        {
            return ExistingProfiles().Except(before, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string[] ExistingProfiles()
        {
            var root = ChromiumHtmlToPdfConverter.ProfilesRootDirectory;

            return Directory.Exists(root) ? Directory.GetDirectories(root) : [];
        }
    }
}
