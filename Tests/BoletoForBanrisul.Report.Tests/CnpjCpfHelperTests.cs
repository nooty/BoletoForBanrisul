using BoletoForBanrisul.Report.Helpers;

namespace BoletoForBanrisul.Report.Tests
{
    [Trait("Category", "Unit")]
    public class CnpjCpfHelperTests
    {
        [Fact]
        public void Format_ElevenDigits_FormatsAsCpf()
        {
            Assert.Equal("000.000.001-91", CnpjCpfHelper.Format("00000000191"));
        }

        [Fact]
        public void Format_FourteenDigits_FormatsAsCnpj()
        {
            Assert.Equal("92.702.067/0001-96", CnpjCpfHelper.Format("92702067000196"));
        }

        [Theory]
        [InlineData("123")]
        [InlineData("123456789012345")]
        public void Format_AnUnrecognizedLength_ReturnsTheInputUnchanged(string value)
        {
            Assert.Equal(value, CnpjCpfHelper.Format(value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Format_NullOrWhitespace_ReturnsEmptyString(string? value)
        {
            Assert.Equal("", CnpjCpfHelper.Format(value!));
        }
    }
}
