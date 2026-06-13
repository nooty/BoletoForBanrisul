using BoletoForBanrisul.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RazorLight;
using BoletoForBanrisul.Report;
using BoletoForBanrisul.Report.Utils;
using Moq;
using System.IO.Compression;
using BoletoForBanrisul.Core.Models.Types;

namespace Test
{
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

        [IntegrationTheory]
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
                Beneficiary = new BeneficiarySettings { 
                    Name = "Beneficiario Teste", 
                    CpfCnpj = "23456789000000" 
                }
            };

            var pdfConverterMock = new Mock<IPdfConverter>();
            pdfConverterMock.Setup(p => p.ConvertToPdfAsync(It.IsAny<string>())).Returns(
                Task.FromResult(new byte[] { 1, 2, 3, 4 })
            );

            var boletoReport = new BoletoReport(printSettings, pdfConverterMock.Object);

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
