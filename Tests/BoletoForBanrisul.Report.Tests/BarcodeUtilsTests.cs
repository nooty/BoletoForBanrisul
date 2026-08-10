using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using BoletoForBanrisul.Report.Utils;
using ZXing;
using ZXing.Common;

namespace BoletoForBanrisul.Report.Tests
{
    [Trait("Category", "Unit")]
    public class BarcodeUtilsTests
    {
        private const string BarcodePayload = "04198861100000150502100100000010000644524040";
        private const string QRCodePayload = "00020126580014BR.GOV.BCB.PIX0136123e4567-e12b-12d1-a456-426655440000520400005303986540510.005802BR5913Fulano de Tal6008BRASILIA62070503***6304ABCD";

        private static readonly Regex RectRegex = new(
            "<rect x=\"(\\d+)\" y=\"(\\d+)\" width=\"(\\d+)\" height=\"(\\d+)\"", RegexOptions.Compiled);
        private static readonly Regex ViewBoxRegex = new(
            "viewBox=\"0 0 (\\d+) (\\d+)\"", RegexOptions.Compiled);

        [Fact]
        public void GenerateBarcodeAsSvg_ReturnsAnSvgDataUri()
        {
            var markup = DecodeSvg(BarcodeUtils.GenerateBarcodeAsSvg(BarcodePayload));

            Assert.Contains("<svg ", markup);
            Assert.Contains("viewBox=", markup);
            Assert.Contains("<rect", markup);
            AssertStretchesToFill(markup);
        }

        [Fact]
        public void GenerateQRCodeAsSvg_ReturnsAnSvgDataUri()
        {
            var markup = DecodeSvg(BarcodeUtils.GenerateQRCodeAsSvg(QRCodePayload));

            Assert.Contains("<svg ", markup);
            Assert.Contains("viewBox=", markup);
            Assert.Contains("<rect", markup);
            AssertStretchesToFill(markup);
        }

        [Fact]
        public void GenerateBarcodeAsSvg_ProducesWellFormedXml()
        {
            AssertIsWellFormedXml(DecodeSvg(BarcodeUtils.GenerateBarcodeAsSvg(BarcodePayload)));
        }

        [Fact]
        public void GenerateQRCodeAsSvg_ProducesWellFormedXml()
        {
            AssertIsWellFormedXml(DecodeSvg(BarcodeUtils.GenerateQRCodeAsSvg(QRCodePayload)));
        }

        [Fact]
        public void GenerateQRCodeAsSvg_ProducesASquareImage()
        {
            var markup = DecodeSvg(BarcodeUtils.GenerateQRCodeAsSvg(QRCodePayload));

            var viewBox = ViewBoxRegex.Match(markup);
            var width = int.Parse(viewBox.Groups[1].Value);
            var height = int.Parse(viewBox.Groups[2].Value);

            Assert.Equal(width, height);
        }

        [Fact]
        public void GenerateBarcodeAsSvg_RoundTripsThroughZXing()
        {
            var markup = DecodeSvg(BarcodeUtils.GenerateBarcodeAsSvg(BarcodePayload));

            var text = DecodeRectanglesBack(markup, BarcodeFormat.CODE_128);

            Assert.Equal(BarcodePayload, text);
        }

        [Fact]
        public void GenerateQRCodeAsSvg_RoundTripsThroughZXing()
        {
            var markup = DecodeSvg(BarcodeUtils.GenerateQRCodeAsSvg(QRCodePayload));

            var text = DecodeRectanglesBack(markup, BarcodeFormat.QR_CODE);

            Assert.Equal(QRCodePayload, text);
        }

        /// <summary>
        /// Asserts the image fills its CSS box instead of being letterboxed inside it. Zero
        /// occurrences mean ZXing changed its markup and the Replace in BarcodeUtils silently
        /// no-op'd; more than one would mean a duplicate attribute, which is fatal for the XML
        /// parser that reads the data URI; one with any other value would letterbox the image.
        /// </summary>
        private static void AssertStretchesToFill(string markup)
        {
            Assert.Equal(1, Regex.Count(markup, "preserveAspectRatio"));
            Assert.Contains("preserveAspectRatio=\"none\"", markup);
        }

        /// <summary>
        /// Parses the markup as XML, which is how a browser reads an "image/svg+xml" data URI. A
        /// malformed document does not degrade gracefully there - the image is dropped entirely.
        /// </summary>
        private static void AssertIsWellFormedXml(string markup)
        {
            // ZXing emits a DOCTYPE pointing at the SVG 1.1 DTD; ignore it rather than fetch it.
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };

            using var reader = XmlReader.Create(new StringReader(markup), settings);
            while (reader.Read())
            {
            }
        }

        private static string DecodeSvg(string dataUri)
        {
            const string prefix = "data:image/svg+xml;base64,";
            Assert.StartsWith(prefix, dataUri);

            var bytes = Convert.FromBase64String(dataUri.Substring(prefix.Length));
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Reconstructs the bit matrix from the SVG's &lt;rect&gt; elements and decodes it back with
        /// ZXing.Net, proving the encoded payload survives the SVG round-trip. Every &lt;rect&gt; is a
        /// dark module - ZXing's SVG has no background rect (it relies on viewport-fill, which Blink
        /// ignores, so the image renders transparent over the template's white page). A quiet-zone margin is
        /// added around the reconstructed matrix before decoding: the SVG itself has none (Margin=0,
        /// NoPadding=true - see BarcodeUtils), matching the real printed boleto, where the surrounding
        /// page and cell padding supply the quiet zone that ZXing's detector needs.
        /// </summary>
        private static string DecodeRectanglesBack(string markup, BarcodeFormat format)
        {
            const int margin = 10;

            var viewBox = ViewBoxRegex.Match(markup);
            var width = int.Parse(viewBox.Groups[1].Value);
            var height = int.Parse(viewBox.Groups[2].Value);

            var dark = new bool[width, height];
            foreach (Match m in RectRegex.Matches(markup))
            {
                int x = int.Parse(m.Groups[1].Value);
                int y = int.Parse(m.Groups[2].Value);
                int w = int.Parse(m.Groups[3].Value);
                int h = int.Parse(m.Groups[4].Value);

                for (int yy = y; yy < y + h; yy++)
                {
                    for (int xx = x; xx < x + w; xx++)
                    {
                        dark[xx, yy] = true;
                    }
                }
            }

            int paddedWidth = width + margin * 2;
            int paddedHeight = height + margin * 2;
            var luminance = new byte[paddedWidth * paddedHeight];
            for (int i = 0; i < luminance.Length; i++)
            {
                luminance[i] = 255;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (dark[x, y])
                    {
                        luminance[(y + margin) * paddedWidth + (x + margin)] = 0;
                    }
                }
            }

            var source = new RGBLuminanceSource(luminance, paddedWidth, paddedHeight, RGBLuminanceSource.BitmapFormat.Gray8);
            var reader = new BarcodeReaderGeneric
            {
                Options = new DecodingOptions
                {
                    PossibleFormats = new[] { format },
                    TryHarder = true,
                    PureBarcode = true
                }
            };

            var result = reader.Decode(source) ?? throw new InvalidOperationException("ZXing failed to decode the reconstructed barcode.");
            return result.Text;
        }
    }
}
