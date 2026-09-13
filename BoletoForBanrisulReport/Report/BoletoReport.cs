using BoletoForBanrisul.Core.Helpers;
using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Report.Utils;
using BoletoForBanrisul.Report.ViewModels;
using RazorLight;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report
{
    /// <summary>
    /// Generates boleto PDFs.
    /// </summary>
    /// <remarks>
    /// Once built it is immutable and keeps no state between calls, so it is safe for concurrent use.
    /// It synchronises nothing of its own: RazorLight already serialises template compilation per key,
    /// and conversion concurrency is decided by the <see cref="IPdfConverter"/> - serialising here
    /// would override its configuration.
    /// </remarks>
    public class BoletoReport : IBoletoReport
    {
        private readonly PrintSettings _settings;
        private readonly IPdfConverter _pdfConverter;
        private readonly IBoletoTemplateRenderer _templateRenderer;
        private readonly bool _ownsPdfConverter;
        private readonly string _logoResource = "BoletoForBanrisul.Report.Resources.Banrisul.png";

        private bool _disposed;

        /// <summary>
        /// Creates the generator with the embedded template and a converter supplied by the caller, who
        /// remains responsible for disposing it.
        /// </summary>
        /// <param name="settings">Print settings.</param>
        /// <param name="pdfConverter">HTML to PDF converter.</param>
        public BoletoReport(PrintSettings settings, IPdfConverter pdfConverter)
            : this(settings, pdfConverter, RazorBoletoTemplateRenderer.Default, ownsPdfConverter: false)
        {
        }

        /// <summary>
        /// Creates the generator with a renderer and a converter supplied by the caller, who remains
        /// responsible for disposing them.
        /// </summary>
        /// <param name="settings">Print settings.</param>
        /// <param name="pdfConverter">HTML to PDF converter.</param>
        /// <param name="templateRenderer">Renderer for the boleto's HTML.</param>
        public BoletoReport(PrintSettings settings, IPdfConverter pdfConverter, IBoletoTemplateRenderer templateRenderer)
            : this(settings, pdfConverter, templateRenderer, ownsPdfConverter: false)
        {
        }

        internal BoletoReport(PrintSettings settings, IPdfConverter pdfConverter, IBoletoTemplateRenderer templateRenderer, bool ownsPdfConverter)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _pdfConverter = pdfConverter ?? throw new ArgumentNullException(nameof(pdfConverter));
            _templateRenderer = templateRenderer ?? throw new ArgumentNullException(nameof(templateRenderer));
            _ownsPdfConverter = ownsPdfConverter;

            ValidateSettings();
        }

        /// <summary>
        /// The renderer in use. It exists so that sharing is verifiable in tests.
        /// </summary>
        internal IBoletoTemplateRenderer TemplateRenderer => _templateRenderer;

        /// <inheritdoc />
        public async Task<byte[]> PrintAsync(Boleto boleto, CancellationToken cancellationToken = default)
        {
            if (boleto == null)
                throw new ArgumentNullException(nameof(boleto));

            ThrowIfDisposed();

            var boletoHtml = await RenderBoletoHtml(boleto, cancellationToken);

            return await _pdfConverter.ConvertToPdfAsync(boletoHtml, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<byte[]> ZipBoletosAsync(IEnumerable<Boleto> boletos, CancellationToken cancellationToken = default)
        {
            if (boletos == null)
                throw new ArgumentNullException(nameof(boletos));

            ThrowIfDisposed();

            using var zipStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var boleto in boletos)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var pdfBytes = await PrintAsync(boleto, cancellationToken);
                    var zipEntryName = GetUniqueFileNameForBoleto(boleto, usedFileNames);

                    var zipEntry = zipArchive.CreateEntry(zipEntryName);

                    using var entryStream = zipEntry.Open();
                    await entryStream.WriteAsync(pdfBytes, 0, pdfBytes.Length, cancellationToken);

                }
            }

            return zipStream.ToArray();
        }

        private string GetUniqueFileNameForBoleto(Boleto boleto, HashSet<string> usedFileNames)
        {
            var fileName = GetFileNameForBoleto(boleto);

            if (usedFileNames.Add(fileName))
                return fileName;

            var extension = Path.GetExtension(fileName);
            var baseName = Path.GetFileNameWithoutExtension(fileName);

            for (var counter = 2; ; counter++)
            {
                var candidate = $"{baseName} ({counter}){extension}";

                if (usedFileNames.Add(candidate))
                    return candidate;
            }
        }

        private string GetFileNameForBoleto(Boleto boleto)
        {
            var fileName = _settings.FileNameTemplate
                .Replace("{NossoNumero}", SanitizeFileName(boleto.Titulo.NossoNumero ?? "unknown"))
                .Replace("{SeuNumero}", SanitizeFileName(boleto.Titulo.SeuNumero))
                .Replace("{DataVencimento}", boleto.Titulo.DataVencimento.ToString("yyyyMMdd"))
                .Replace("{DataEmissao}", boleto.Titulo.DataEmissao.ToString("yyyyMMdd"))
                .Replace("{CpfCnpjPagador}", SanitizeFileName(ExtractDocumentCharacters(boleto.Titulo.Pagador.CpfCnpj)))
                .Replace("{NomePagador}", SanitizeFileName(boleto.Titulo.Pagador.Nome))
                .Replace("{NomeBeneficiario}", SanitizeFileName(_settings.Beneficiary.Name))
                .Replace("{CpfCnpjBeneficiario}", SanitizeFileName(ExtractDocumentCharacters(_settings.Beneficiary.CpfCnpj)));

            return $"{fileName}.pdf";
        }

        private string SanitizeFileName(string input)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", input.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        private string ExtractDocumentCharacters(string input)
        {
            return new string(input.Where(c =>
                (c >= '0' && c <= '9') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z')).ToArray());
        }

        private async Task<string> RenderBoletoHtml(Boleto boleto, CancellationToken cancellationToken)
        {
            PrintViewModel viewModel = GenerateViewModel(boleto);

            return await _templateRenderer.RenderAsync(viewModel, cancellationToken);
        }

        private PrintViewModel GenerateViewModel(Boleto boleto)
        {
            var barCode =
                boleto.Titulo.CodigoBarras != null &&
                !string.IsNullOrEmpty(boleto.Titulo.CodigoBarras) ? boleto.Titulo.CodigoBarras : BoletoHelper.GetBarcode(boleto);

            var digitableLine =
                boleto.Titulo.LinhaDigitavel != null &&
                !string.IsNullOrEmpty(boleto.Titulo.LinhaDigitavel) ? boleto.Titulo.LinhaDigitavel : BoletoHelper.GetDigitableLine(boleto);

            return new PrintViewModel()
            {
                LogoPath = string.IsNullOrEmpty(_settings.LogoFilePath) ?
                    GetBase64Image(GetLogoFromResource()) :
                    GetBase64Image(File.ReadAllBytes(_settings.LogoFilePath)),
                Barcode = BarcodeUtils.GenerateBarcodeAsSvg(barCode),
                DigitableLine = BoletoHelper.FormatDigitableLine(digitableLine),
                QRCode = !string.IsNullOrEmpty(boleto.Titulo.Hibrido?.CopiaCola) ?
                    BarcodeUtils.GenerateQRCodeAsSvg(boleto.Titulo.Hibrido?.CopiaCola ?? "") : "",
                PaymentLocation = _settings.PaymentLocation,
                Beneficiary = _settings.Beneficiary,
                Boleto = boleto
            };
        }

        private byte[] GetLogoFromResource()
        {
            using var logoStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(_logoResource)
                ?? throw new InvalidOperationException($"Recurso de logo não encontrado. Resource={_logoResource}");

            using var memoryStream = new MemoryStream();
            logoStream.CopyTo(memoryStream);

            return memoryStream.ToArray();
        }

        private string GetBase64Image(byte[] imageBytes, string imageType = "png")
        {
            string base64Image = $"data:image/{imageType};base64,{Convert.ToBase64String(imageBytes)}";
            return base64Image;
        }

        private void ValidateSettings()
        {
            if (!string.IsNullOrEmpty(_settings.LogoFilePath) && !File.Exists(_settings.LogoFilePath))
                throw new InvalidOperationException($"Arquivo para o logo do boleto inválido. File={_settings.LogoFilePath}");

            // Template validation does not live here: what knows the template's origin is the
            // IBoletoTemplateRenderer, and it validates in its own constructor.
            if (string.IsNullOrEmpty(_settings.FileNameTemplate))
                throw new InvalidOperationException("Template para nome de boleto exportado não foi configurado.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BoletoReport));
        }

        /// <summary>
        /// Releases the generator's resources.
        /// </summary>
        /// <remarks>
        /// It disposes the PDF converter only when this object created it - that is, when it came from
        /// <see cref="BoletoReportBuilder.Build"/> without
        /// <see cref="BoletoReportBuilder.SetPdfConverter"/>. A converter received ready-made is
        /// usually shared, and disposing it here would leave the next reports without one. The same
        /// holds for the renderer, which is never disposed here.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_ownsPdfConverter && _pdfConverter is IDisposable disposableConverter)
                disposableConverter.Dispose();
        }
    }


}
