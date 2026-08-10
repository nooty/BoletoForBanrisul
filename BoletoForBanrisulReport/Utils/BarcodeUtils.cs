using System.Text;
using ZXing;
using ZXing.Common;

namespace BoletoForBanrisul.Report.Utils
{
    /// <summary>
    /// Utility class for generating barcode and QR code images as SVG data URIs.
    /// </summary>
    public static class BarcodeUtils
    {
        /// <summary>
        /// Generates an Interleaved 2 of 5 barcode from the given code, encoded as an SVG data URI.
        /// This is the symbology FEBRABAN specifies for the 44-digit boleto barcode, so the code is
        /// expected to be numeric and of even length.
        /// </summary>
        /// <param name="code">The value to encode as a barcode.</param>
        /// <returns>A "data:image/svg+xml;base64,..." URI containing the rendered barcode.</returns>
        public static string GenerateBarcodeAsSvg(string code)
        {
            // Dimensions converted to pixels, based on 96 DPI, matching the 103mm x 13mm printed
            // size. Interleaved 2 of 5 spreads the 44 digits over 405 modules, wider than the ~389
            // requested here, so ZXing keeps its natural width and the template's CSS scales it back
            // to 103mm - which puts the narrow bar at 0.254mm, the minimum FEBRABAN allows.
            int widthInPixels = (int)(103 * 96 / 25.4); // ~389 pixels.
            int heightInPixels = (int)(13 * 96 / 25.4); // ~49 pixels.

            var writer = new BarcodeWriterSvg
            {
                Format = BarcodeFormat.ITF,
                Options = new EncodingOptions
                {
                    Height = heightInPixels,
                    Width = widthInPixels,
                    Margin = 0,
                    NoPadding = true,
                    PureBarcode = true
                }
            };

            return EncodeAsSvgDataUri(writer, code);
        }

        /// <summary>
        /// Generates a QR code from the given code, encoded as an SVG data URI.
        /// </summary>
        /// <param name="code">The value to encode as a QR code.</param>
        /// <returns>A "data:image/svg+xml;base64,..." URI containing the rendered QR code.</returns>
        public static string GenerateQRCodeAsSvg(string code)
        {
            // Dimensions converted to pixels, based on 96 DPI.
            int sizeInPixels = (int)(25 * 96 / 25.4); // ~94 pixels for 25mm.

            var writer = new BarcodeWriterSvg
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

            return EncodeAsSvgDataUri(writer, code);
        }

        /// <summary>
        /// Encodes <paramref name="code"/> with the given writer, patches in
        /// <c>preserveAspectRatio="none"</c> and wraps the result as a base64 SVG data URI.
        /// </summary>
        private static string EncodeAsSvgDataUri(BarcodeWriterSvg writer, string code)
        {
            string svg = writer.Write(code).ToString();

            // preserveAspectRatio="none" is required: with NoPadding=true the encoded matrix
            // (especially the QR code) does not match the requested Width/Height, and the report
            // template stretches the <img> to a fixed mm size via CSS. The SVG default
            // (xMidYMid meet) would instead letterbox and shrink the image inside that box.
            // "<svg " occurs exactly once in ZXing's output - the DOCTYPE only contains
            // "svg PUBLIC" / "svg11.dtd", neither of which matches this pattern.
            // The guard keeps a future ZXing that emits its own preserveAspectRatio from ending up
            // with the attribute twice: the data URI is parsed as XML, where a duplicate attribute
            // is fatal and would drop the image entirely rather than merely misplace it.
            if (svg.IndexOf("preserveAspectRatio", StringComparison.Ordinal) < 0)
            {
                svg = svg.Replace("<svg ", "<svg preserveAspectRatio=\"none\" ");
            }

            return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
        }
    }
}
