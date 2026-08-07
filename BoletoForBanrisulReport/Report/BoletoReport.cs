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
    /// Gerador de PDF de boletos.
    /// </summary>
    /// <remarks>
    /// Depois de construído é imutável e não guarda estado entre chamadas, então pode ser usado
    /// concorrentemente. Não sincroniza nada por conta própria: o RazorLight já serializa a
    /// compilação de template por chave, e a concorrência da conversão é decidida pelo
    /// <see cref="IPdfConverter"/> - serializar aqui anularia a configuração dele.
    /// </remarks>
    public class BoletoReport : IBoletoReport
    {
        private readonly PrintSettings _settings;
        private readonly IPdfConverter _pdfConverter;
        private readonly bool _ownsPdfConverter;
        private readonly RazorLightEngine _razorEngine;
        private readonly string _templateKey;
        private readonly string _resourcesNamespace = "BoletoForBanrisul.Report.Resources";
        private readonly string _templateResourceName = "Boleto.cshtml";
        private readonly string _logoResource = "BoletoForBanrisul.Report.Resources.Banrisul.png";

        private bool _disposed;

        /// <summary>
        /// Cria o gerador com um conversor fornecido pelo chamador, que continua responsável por
        /// descartá-lo.
        /// </summary>
        /// <param name="settings">Configurações de impressão.</param>
        /// <param name="pdfConverter">Conversor de HTML para PDF.</param>
        public BoletoReport(PrintSettings settings, IPdfConverter pdfConverter)
            : this(settings, pdfConverter, ownsPdfConverter: false)
        {
        }

        internal BoletoReport(PrintSettings settings, IPdfConverter pdfConverter, bool ownsPdfConverter)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _pdfConverter = pdfConverter ?? throw new ArgumentNullException(nameof(pdfConverter));
            _ownsPdfConverter = ownsPdfConverter;

            ValidateSettings();

            (_razorEngine, _templateKey) = BuildRazorEngine();
        }

        private (RazorLightEngine Engine, string TemplateKey) BuildRazorEngine()
        {
            if (string.IsNullOrEmpty(_settings.TemplateDirectoryPath))
            {
                var engine = new RazorLightEngineBuilder()
                    .UseEmbeddedResourcesProject(typeof(BoletoReport).Assembly, _resourcesNamespace)
                    .SetOperatingAssembly(Assembly.GetExecutingAssembly())
                    .UseMemoryCachingProvider()
                    .Build();

                return (engine, _templateResourceName);
            }

            var fileEngine = new RazorLightEngineBuilder()
                .UseFileSystemProject(_settings.TemplateDirectoryPath)
                .SetOperatingAssembly(Assembly.GetExecutingAssembly())
                .UseMemoryCachingProvider()
                .Build();

            return (fileEngine, _settings.TemplateFileName);
        }

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
                .Replace("{CpfCnpjPagador}", SanitizeFileName(ExtractNumbers(boleto.Titulo.Pagador.CpfCnpj)))
                .Replace("{NomePagador}", SanitizeFileName(boleto.Titulo.Pagador.Nome))
                .Replace("{NomeBeneficiario}", SanitizeFileName(_settings.Beneficiary.Name))
                .Replace("{CpfCnpjBeneficiario}", SanitizeFileName(ExtractNumbers(_settings.Beneficiary.CpfCnpj)));

            return $"{fileName}.pdf";
        }

        private string SanitizeFileName(string input)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", input.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        private string ExtractNumbers(string input)
        {
            return new string(input.Where(char.IsDigit).ToArray());
        }

        private async Task<string> RenderBoletoHtml(Boleto boleto, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PrintViewModel viewModel = GenerateViewModel(boleto);

            // Sem lock: o RazorTemplateCompiler já serializa a compilação por chave, e o cache de
            // templates é um IMemoryCache.
            return await _razorEngine.CompileRenderAsync(_templateKey, viewModel);
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
                Barcode = BarcodeUtils.GenerateBarcodeAsBase64(barCode),
                DigitableLine = BoletoHelper.FormatDigitableLine(digitableLine),
                QRCode = !string.IsNullOrEmpty(boleto.Titulo.Hibrido?.CopiaCola) ?
                    BarcodeUtils.GenerateQRCodeAsBase64(boleto.Titulo.Hibrido?.CopiaCola ?? "") : "",
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

            if (!string.IsNullOrEmpty(_settings.TemplateDirectoryPath))
            {
                if (!Directory.Exists(_settings.TemplateDirectoryPath))
                    throw new InvalidOperationException($"Caminho do arquivo de template inválido. Path={_settings.TemplateDirectoryPath}");

                if (string.IsNullOrEmpty(_settings.TemplateFileName))
                    throw new InvalidOperationException("Nome do arquivo de template não foi configurado.");

                var templateFilePath = Path.Combine(_settings.TemplateDirectoryPath, _settings.TemplateFileName);

                if (!File.Exists(templateFilePath))
                    throw new InvalidOperationException($"Arquivo de template do boleto inválido. File={templateFilePath}");
            }

            if (string.IsNullOrEmpty(_settings.FileNameTemplate))
                throw new InvalidOperationException("Template para nome de boleto exportado não foi configurado.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BoletoReport));
        }

        /// <summary>
        /// Libera os recursos do gerador.
        /// </summary>
        /// <remarks>
        /// Descarta o conversor de PDF apenas quando foi este objeto que o criou - isto é, quando
        /// veio do <see cref="BoletoReportBuilder.Build"/> sem
        /// <see cref="BoletoReportBuilder.SetPdfConverter"/>. Um conversor recebido pronto costuma
        /// ser compartilhado, e descartá-lo aqui deixaria os próximos relatórios sem conversor.
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
