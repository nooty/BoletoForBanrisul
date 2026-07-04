using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;

namespace BoletoForBanrisul.Report.Utils
{
    /// <summary>
    /// Classe utilitária para gerar códigos de barras e QR Codes em formato Base64.
    /// </summary>
    public static class BarcodeUtils
	{
        /// <summary>
        /// Gera um código de barras CODE 128 em formato Base64 a partir de um código fornecido.
        /// </summary>
        /// <param name="code">O código a ser convertido em um código de barras.</param>
        /// <returns>Uma string Base64 representando a imagem do código de barras gerado.</returns>
        public static string GenerateBarcodeAsBase64(string code)
		{
			// Conversão das dimensões para pixels (com base em 96 DPI).
			int widthInPixels = (int)(103 * 96 / 25.4); // Aproximadamente 389 pixels.
			int heightInPixels = (int)(13 * 96 / 25.4); // Aproximadamente 49 pixels.

			var barcodeWriter = new ZXing.SkiaSharp.BarcodeWriter
			{
				Format = BarcodeFormat.CODE_128,
				Options = new EncodingOptions
				{
					Height = heightInPixels,
					Width = widthInPixels,
					Margin = 0,
					NoPadding = true,
					PureBarcode = true
				}
			};

			using (var barcodeImage = barcodeWriter.Write(code))
			{
				using (var image = SKImage.FromBitmap(barcodeImage))
				using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
				{
					return $"data:image/png;base64,{Convert.ToBase64String(data.ToArray())}";
				}
			}
		}

        /// <summary>
        /// Gera um QR Code em formato Base64 a partir de um código fornecido.
        /// </summary>
        /// <param name="code">O código a ser convertido em um QR Code.</param>
        /// <returns>Uma string Base64 representando a imagem do QR Code gerado.</returns>
        public static string GenerateQRCodeAsBase64(string code)
        {
            // Conversão das dimensões para pixels (com base em 96 DPI).
            int sizeInPixels = (int)(25 * 96 / 25.4); // Aproximadamente 94 pixels para 25mm.

            var qrCodeWriter = new ZXing.SkiaSharp.BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new EncodingOptions
                {
                    Height = sizeInPixels,
                    Width = sizeInPixels,
                    Margin = 0, 
                    NoPadding = true
                }
            };

            using (var qrCodeImage = qrCodeWriter.Write(code))
            {
                using (var image = SKImage.FromBitmap(qrCodeImage))
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    return $"data:image/png;base64,{Convert.ToBase64String(data.ToArray())}";
                }
            }
        }

    }
}
