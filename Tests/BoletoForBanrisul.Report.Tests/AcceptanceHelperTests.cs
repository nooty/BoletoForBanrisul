using BoletoForBanrisul.Core.Models.Types;
using BoletoForBanrisul.Report.Helpers;

namespace BoletoForBanrisul.Report.Tests
{
    [Trait("Category", "Unit")]
    public class AcceptanceHelperTests
    {
        public static IEnumerable<object[]> AllAceiteValues() =>
            Enum.GetValues<Aceite>().Select(value => new object[] { value });

        [Theory]
        [MemberData(nameof(AllAceiteValues))]
        public void GetAbbreviation_IsDefinedForEveryAceiteValue(Aceite acceptance)
        {
            var exception = Record.Exception(() => AcceptanceHelper.GetAbbreviation(acceptance));

            Assert.Null(exception);
        }

        [Theory]
        [InlineData(Aceite.Sim, "A")]
        [InlineData(Aceite.Nao, "N")]
        public void GetAbbreviation_ReturnsTheExpectedAbbreviation(Aceite acceptance, string expected)
        {
            Assert.Equal(expected, AcceptanceHelper.GetAbbreviation(acceptance));
        }

        [Fact]
        public void GetAbbreviation_Throws_ForAnUndefinedValue()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AcceptanceHelper.GetAbbreviation((Aceite)(-1)));
        }
    }
}
