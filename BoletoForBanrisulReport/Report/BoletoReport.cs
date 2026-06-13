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
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report
{
    public class BoletoReport : IBoletoReport
    {
        private readonly SemaphoreSlim _printSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _zipSemaphore = new SemaphoreSlim(1, 1);

        private PrintSettings _settings;
        private IPdfConverter _pdfConverter;
        private readonly RazorLightEngine _razorEngine;
        private readonly string _templateKey;
        private readonly string _resourcesNamespace = "BoletoForBanrisul.Report.Resources";
        private readonly string _templateResourceName = "Boleto.cshtml";
        private readonly string _logoResource = "BoletoForBanrisul.Report.Resources.Banrisul.png";

        public BoletoReport(PrintSettings settings, IPdfConverter pdfConverter)
        {
            _settings = settings;
            _pdfConverter = pdfConverter;
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

        public async Task<byte[]> PrintAsync(Boleto boleto)
        {
            await _printSemaphore.WaitAsync();

            try
            {
                var boletoHtml = await RenderBoletoHtml(boleto);

                return await _pdfConverter.ConvertToPdfAsync(boletoHtml);
            }
            finally
            {
                _printSemaphore.Release();
            }
        }

        public async Task<byte[]> ZipBoletosAsync(IEnumerable<Boleto> boletos)
        {
            await _zipSemaphore.WaitAsync();

            try
            {
                using var zipStream = new MemoryStream();
                using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    foreach (var boleto in boletos)
                    {
                        var pdfBytes = await PrintAsync(boleto);
                        var zipEntryName = GetFileNameForBoleto(boleto);

                        var zipEntry = zipArchive.CreateEntry(zipEntryName);

                        using var entryStream = zipEntry.Open();
                        await entryStream.WriteAsync(pdfBytes, 0, pdfBytes.Length);

                    }
                }

                return zipStream.ToArray();
            }
            finally
            {
                _zipSemaphore.Release();
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

        private async Task<string> RenderBoletoHtml(Boleto boleto)
        {
            PrintViewModel viewModel = GenerateViewModel(boleto);
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
            var logoStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(_logoResource);
            byte[] logoBytes;

            using var memoryStream = new MemoryStream();
            logoStream.CopyTo(memoryStream);
            logoBytes = memoryStream.ToArray();

            return logoBytes;
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

            if (!string.IsNullOrEmpty(_settings.TemplateDirectoryPath) && !Directory.Exists(_settings.TemplateDirectoryPath))
                throw new InvalidOperationException($"Caminho do arquivo de template inválido. Path={_settings.TemplateDirectoryPath}");

            var templateFilePath = Path.Combine(_settings.TemplateDirectoryPath, _settings.TemplateFileName);

            if (!string.IsNullOrEmpty(_settings.TemplateFileName) && !File.Exists(templateFilePath))
                throw new InvalidOperationException($"Arquivo de template do boleto inválido. File={templateFilePath}");

            if (string.IsNullOrEmpty(_settings.FileNameTemplate))
                throw new InvalidOperationException("Template para nome de boleto exportado não foi configurado.");
        }
    }


}
