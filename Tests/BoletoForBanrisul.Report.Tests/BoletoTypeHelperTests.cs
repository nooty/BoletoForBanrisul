using BoletoForBanrisul.Core.Models.Types;
using BoletoForBanrisul.Report.Helpers;

namespace BoletoForBanrisul.Report.Tests
{
    [Trait("Category", "Unit")]
    public class BoletoTypeHelperTests
    {
        public static IEnumerable<object[]> AllEspecieValues() =>
            Enum.GetValues<Especie>().Select(value => new object[] { value });

        [Theory]
        [MemberData(nameof(AllEspecieValues))]
        public void GetAbbreviation_IsDefinedForEveryEspecieValue(Especie type)
        {
            // Driven by Enum.GetValues so a new Especie member without a matching switch arm fails this test
            // instead of silently falling through to the switch's ArgumentOutOfRangeException at report time.
            var exception = Record.Exception(() => BoletoTypeHelper.GetAbbreviation(type));

            Assert.Null(exception);
        }

        [Theory]
        [InlineData(Especie.DuplicataMercantil, "DM")]
        [InlineData(Especie.DuplicataServico, "DS")]
        [InlineData(Especie.CartaoCredito, "CC")]
        [InlineData(Especie.BoletoProposta, "BDP")]
        [InlineData(Especie.Outros, "Outros")]
        public void GetAbbreviation_ReturnsTheExpectedAbbreviation(Especie type, string expected)
        {
            Assert.Equal(expected, BoletoTypeHelper.GetAbbreviation(type));
        }

        [Fact]
        public void GetAbbreviation_Throws_ForAnUndefinedValue()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BoletoTypeHelper.GetAbbreviation((Especie)(-1)));
        }
    }
}
