using BoletoForBanrisul.Core.Models.Types;
using BoletoForBanrisul.Report.Helpers;

namespace BoletoForBanrisul.Report.Tests
{
    [Trait("Category", "Unit")]
    public class WalletTypeHelperTests
    {
        public static IEnumerable<object[]> AllCarteiraValues() =>
            Enum.GetValues<Carteira>().Select(value => new object[] { value });

        [Theory]
        [MemberData(nameof(AllCarteiraValues))]
        public void GetAbbreviation_PadsEveryCarteiraValue_ToTwoDigits(Carteira code)
        {
            var result = WalletTypeHelper.GetAbbreviation(code);

            Assert.Equal(2, result.Length);
            Assert.Equal(((int)code).ToString().PadLeft(2, '0'), result);
        }

        [Fact]
        public void GetAbbreviation_Null_ReturnsEmptyString()
        {
            Assert.Equal("", WalletTypeHelper.GetAbbreviation(null));
        }

        [Theory]
        [InlineData(Carteira.CobrancaSimples, "01")]
        [InlineData(Carteira.CobrancaVinculada, "02")]
        [InlineData(Carteira.CobrancaCaucionada, "03")]
        [InlineData(Carteira.Desconto, "04")]
        [InlineData(Carteira.Vendor, "05")]
        public void GetAbbreviation_ReturnsTheExpectedTwoDigitCode(Carteira code, string expected)
        {
            Assert.Equal(expected, WalletTypeHelper.GetAbbreviation(code));
        }
    }
}
