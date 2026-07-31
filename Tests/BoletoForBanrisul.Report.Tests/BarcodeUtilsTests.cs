using BoletoForBanrisul.Report.Utils;

namespace BoletoForBanrisul.Report.Tests
{
    [Trait("Category", "Unit")]
    public class BarcodeUtilsTests
    {
        private static readonly byte[] PngMagicNumber = { 0x89, 0x50, 0x4E, 0x47 };

        [Fact]
        public void GenerateBarcodeAsBase64_ReturnsAPngDataUri()
        {
            var result = BarcodeUtils.GenerateBarcodeAsBase64("04198861100000150502100100000010000644524040");

            AssertIsPngDataUri(result);
        }

        [Fact]
        public void GenerateQRCodeAsBase64_ReturnsAPngDataUri()
        {
            var result = BarcodeUtils.GenerateQRCodeAsBase64("00020126580014BR.GOV.BCB.PIX");

            AssertIsPngDataUri(result);
        }

        private static void AssertIsPngDataUri(string dataUri)
        {
            const string prefix = "data:image/png;base64,";
            Assert.StartsWith(prefix, dataUri);

            var bytes = Convert.FromBase64String(dataUri.Substring(prefix.Length));
            Assert.Equal(PngMagicNumber, bytes.Take(PngMagicNumber.Length));
        }
    }
}
