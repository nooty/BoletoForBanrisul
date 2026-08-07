using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Report.Utils;

namespace BoletoForBanrisul.Report.Tests
{
    /// <summary>
    /// The token has to reach <see cref="Utils.IPdfConverter"/>, where the time is spent and where a
    /// conversion can wedge. A report that accepts a token and then drops it looks cancellable and
    /// is not.
    /// </summary>
    [Trait("Category", "Unit")]
    public class ReportCancellationTests
    {
        private static Boleto CreateBoleto(string nossoNumero = "123456") => new Boleto
        {
            Titulo = new Titulo
            {
                NossoNumero = nossoNumero,
                SeuNumero = "7890",
                DataVencimento = new DateTime(2026, 10, 5),
                DataEmissao = new DateTime(2026, 9, 30),
                Pagador = new Pagador { CpfCnpj = "12345678900", Nome = "PAGADOR TESTE" }
            }
        };

        private static PrintSettings CreateSettings() => new PrintSettings
        {
            Beneficiary = new BeneficiarySettings { Name = "BENEFICIARIO TESTE", CpfCnpj = "23456789000000" }
        };

        [Fact]
        public async Task PrintAsync_PassesTheCallersTokenToTheConverter()
        {
            var recorder = new RecordingPdfConverter();
            using var report = new BoletoReport(CreateSettings(), recorder);
            using var cts = new CancellationTokenSource();

            await report.PrintAsync(CreateBoleto(), cts.Token);

            Assert.Equal(cts.Token, recorder.LastCancellationToken);
        }

        [Fact]
        public async Task PrintAsync_WithAnAlreadyCancelledToken_Throws()
        {
            var recorder = new RecordingPdfConverter();
            using var report = new BoletoReport(CreateSettings(), recorder);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => report.PrintAsync(CreateBoleto(), cts.Token));
        }

        [Fact]
        public async Task ZipBoletosAsync_WithAnAlreadyCancelledToken_ConvertsNothing()
        {
            var recorder = new RecordingPdfConverter();
            using var report = new BoletoReport(CreateSettings(), recorder);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var boletos = new List<Boleto> { CreateBoleto("1"), CreateBoleto("2") };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => report.ZipBoletosAsync(boletos, cts.Token));

            Assert.Empty(recorder.AllHtml);
        }

        [Fact]
        public async Task ZipBoletosAsync_CancelledMidway_StopsConvertingTheRemainingBoletos()
        {
            var recorder = new RecordingPdfConverter();
            using var report = new BoletoReport(CreateSettings(), recorder);
            using var cts = new CancellationTokenSource();

            // Cancels once the first boleto is converted: the loop must abort instead of grinding
            // through the rest of the batch.
            var boletos = new[] { "1", "2", "3" }.Select(number => CreateBoleto(number)).ToList();

            var cancellingConverter = new CancelAfterFirstConverter(recorder, cts);

            using var cancellingReport = new BoletoReport(CreateSettings(), cancellingConverter);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => cancellingReport.ZipBoletosAsync(boletos, cts.Token));

            Assert.Single(recorder.AllHtml);
        }

        [Fact]
        public async Task PrintAsync_AfterDispose_ThrowsObjectDisposed()
        {
            var report = new BoletoReport(CreateSettings(), new RecordingPdfConverter());
            report.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => report.PrintAsync(CreateBoleto()));
        }

        private sealed class CancelAfterFirstConverter : IPdfConverter
        {
            private readonly RecordingPdfConverter _inner;
            private readonly CancellationTokenSource _cts;

            internal CancelAfterFirstConverter(RecordingPdfConverter inner, CancellationTokenSource cts)
            {
                _inner = inner;
                _cts = cts;
            }

            public async Task<byte[]> ConvertToPdfAsync(string html, CancellationToken cancellationToken = default)
            {
                var pdf = await _inner.ConvertToPdfAsync(html, cancellationToken);
                _cts.Cancel();

                return pdf;
            }
        }
    }
}
