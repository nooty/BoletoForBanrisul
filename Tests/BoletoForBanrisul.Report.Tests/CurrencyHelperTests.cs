using System.Globalization;
using BoletoForBanrisul.Report.Helpers;

namespace BoletoForBanrisul.Report.Tests
{
    [Trait("Category", "Unit")]
    public class CurrencyHelperTests
    {
        [Fact]
        public void Format_UsesPtBrCurrencyFormat_RegardlessOfTheCurrentCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

                var result = CurrencyHelper.Format(150.50m);

                Assert.Equal(150.50m.ToString("C", CultureInfo.GetCultureInfo("pt-BR")), result);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void Format_NegativeValue_IsFormatted()
        {
            var result = CurrencyHelper.Format(-10m);

            Assert.Equal((-10m).ToString("C", CultureInfo.GetCultureInfo("pt-BR")), result);
        }
    }
}
