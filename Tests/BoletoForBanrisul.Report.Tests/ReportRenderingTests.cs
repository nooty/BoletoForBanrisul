using BoletoForBanrisul.Core.Helpers;
using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;

namespace BoletoForBanrisul.Report.Tests
{
    /// <summary>
    /// Renders the real Razor template (<c>Resources/Boleto.cshtml</c>) through a real <see cref="BoletoReport"/>,
    /// substituting only the final PDF conversion step with <see cref="RecordingPdfConverter"/> so no headless
    /// Chrome/Chromium is needed. What each test asserts on is the produced HTML itself.
    /// </summary>
    [Trait("Category", "Unit")]
    public class ReportRenderingTests
    {
        private static Boleto CreateBoleto() => new Boleto
        {
            Titulo = new Titulo
            {
                NossoNumero = "0000038806",
                SeuNumero = "64452-1",
                DataVencimento = new DateTime(2026, 8, 30),
                DataEmissao = new DateTime(2026, 7, 29),
                ValorNominal = 150.50m,
                Especie = Especie.Outros,
                Beneficiario = new Beneficiario { Codigo = "0010000001088" },
                Pagador = new Pagador
                {
                    TipoPessoa = TipoPessoa.PessoaFisica,
                    CpfCnpj = "00000000191",
                    Nome = "PAGADOR FICTICIO",
                    Endereco = "RUA CALDAS JUNIOR 120",
                    Cep = "90010260",
                    Cidade = "PORTO ALEGRE",
                    Uf = "RS",
                    Aceite = Aceite.Sim
                },
                Sacador = new Sacador
                {
                    TipoPessoa = TipoPessoa.PessoaFisica,
                    CpfCnpj = "00000000272",
                    Nome = "SACADOR FICTICIO",
                    Endereco = "RUA CALDAS JUNIOR 120",
                    Cep = "90010260"
                },
                Instrucoes = new Instrucoes
                {
                    Juros = new Juros { Codigo = CodigoJuros.TaxaMensal, Taxa = 1.00m }
                }
            }
        };

        private static PrintSettings CreateSettings() => new PrintSettings
        {
            Beneficiary = new BeneficiarySettings
            {
                Code = "0010000001088",
                Name = "BANRISUL COBRANCA-TESTE",
                CpfCnpj = "92702067000196",
                Address = "AV. DOS ANDRADAS 1234",
                City = "PORTO ALEGRE",
                State = "RS",
                ZipCode = "90010260"
            }
        };

        [Fact]
        public async Task PrintAsync_RendersThePayerLine_WithNameAndFormattedCpf()
        {
            var recorder = new RecordingPdfConverter();
            var report = new BoletoReport(CreateSettings(), recorder);

            await report.PrintAsync(CreateBoleto());

            Assert.Contains("PAGADOR FICTICIO", recorder.LastHtml);
            Assert.Contains("000.000.001-91", recorder.LastHtml); // CnpjCpfHelper.Format("00000000191").
        }

        [Fact]
        public async Task PrintAsync_RendersTheBeneficiaryLine_WithNameAndFormattedCnpj()
        {
            var recorder = new RecordingPdfConverter();
            var report = new BoletoReport(CreateSettings(), recorder);

            await report.PrintAsync(CreateBoleto());

            Assert.Contains("BANRISUL COBRANCA-TESTE", recorder.LastHtml);
            Assert.Contains("92.702.067/0001-96", recorder.LastHtml); // CnpjCpfHelper.Format("92702067000196").
        }

        [Fact]
        public async Task PrintAsync_RendersTheSacadorLine_WithNameAndFormattedCpf()
        {
            var recorder = new RecordingPdfConverter();
            var report = new BoletoReport(CreateSettings(), recorder);

            await report.PrintAsync(CreateBoleto());

            Assert.Contains("SACADOR FICTICIO", recorder.LastHtml);
            Assert.Contains("000.000.002-72", recorder.LastHtml); // CnpjCpfHelper.Format("00000000272").
        }

        [Fact]
        public async Task PrintAsync_RendersTheDigitableLine_Formatted()
        {
            var recorder = new RecordingPdfConverter();
            var boleto = CreateBoleto();
            var report = new BoletoReport(CreateSettings(), recorder);

            await report.PrintAsync(boleto);

            // Titulo.LinhaDigitavel is unset, so BoletoReport falls back to computing it from the boleto -
            // the same helper used here to compute the expectation.
            var expectedDigitableLine = BoletoHelper.FormatDigitableLine(BoletoHelper.GetDigitableLine(boleto));

            Assert.Contains(expectedDigitableLine, recorder.LastHtml);
        }

        [Fact]
        public async Task PrintAsync_EmbedsTheBarcodeImage()
        {
            var recorder = new RecordingPdfConverter();
            var report = new BoletoReport(CreateSettings(), recorder);

            await report.PrintAsync(CreateBoleto());

            Assert.Contains("class=\"barcode\"", recorder.LastHtml);
            Assert.Contains("data:image/png;base64,", recorder.LastHtml);
        }

        [Fact]
        public async Task PrintAsync_RendersAnInstructionLine_WhenNoExplicitMessagesAreSet()
        {
            // Titulo.Mensagens is null here, so Messages.cshtml falls back to
            // InstructionHelper.ConvertInstructionsToMessages(Instrucoes) - the Juros instruction set on
            // CreateBoleto() must show up as a rendered instruction line.
            var recorder = new RecordingPdfConverter();
            var report = new BoletoReport(CreateSettings(), recorder);

            await report.PrintAsync(CreateBoleto());

            Assert.Contains("Juros", recorder.LastHtml);
        }

        [Fact]
        public async Task PrintAsync_RendersExplicitMessages_WhenTituloMensagensIsSet()
        {
            var recorder = new RecordingPdfConverter();
            var boleto = CreateBoleto();
            boleto.Titulo.Mensagens = new List<Mensagem>
            {
                new Mensagem { Linha = 1, Texto = "PAGAMENTO PREFERENCIALMENTE NO BANRISUL" }
            };
            var report = new BoletoReport(CreateSettings(), recorder);

            await report.PrintAsync(boleto);

            Assert.Contains("PAGAMENTO PREFERENCIALMENTE NO BANRISUL", recorder.LastHtml);
        }
    }
}
