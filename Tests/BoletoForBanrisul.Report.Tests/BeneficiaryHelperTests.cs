using BoletoForBanrisul.Report.Helpers;

namespace BoletoForBanrisul.Report.Tests
{
    [Trait("Category", "Unit")]
    public class BeneficiaryHelperTests
    {
        [Fact]
        public void Format_ThirteenCharacterCode_SplitsIntoAgencyAndAccount()
        {
            Assert.Equal("0010 / 000001088", BeneficiaryHelper.Format("0010000001088"));
        }

        [Theory]
        [InlineData("123")]
        [InlineData("123456789012345")]
        public void Format_AnUnrecognizedLength_ReturnsTheInputUnchanged(string value)
        {
            Assert.Equal(value, BeneficiaryHelper.Format(value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Format_NullOrWhitespace_ReturnsEmptyString(string? value)
        {
            Assert.Equal("", BeneficiaryHelper.Format(value!));
        }
    }
}
