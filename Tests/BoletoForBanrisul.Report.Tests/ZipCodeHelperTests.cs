using BoletoForBanrisul.Report.Helpers;

namespace BoletoForBanrisul.Report.Tests
{
    [Trait("Category", "Unit")]
    public class ZipCodeHelperTests
    {
        [Fact]
        public void Format_EightDigits_InsertsTheHyphen()
        {
            Assert.Equal("90010-260", ZipCodeHelper.Format("90010260"));
        }

        [Theory]
        [InlineData("1234567")]
        [InlineData("123456789")]
        public void Format_AnUnrecognizedLength_ReturnsTheInputUnchanged(string value)
        {
            Assert.Equal(value, ZipCodeHelper.Format(value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Format_NullOrWhitespace_ReturnsEmptyString(string? value)
        {
            Assert.Equal("", ZipCodeHelper.Format(value!));
        }
    }
}
