using BoletoForBanrisul.Core.Converters;
using System.Globalization;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class DecimalF2ConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new DecimalF2Converter() } };

        [Fact]
        public void RoundTrips_AValue_WithTwoDecimalPlaces()
        {
            var json = JsonSerializer.Serialize(150.5m, Options);
            Assert.Equal("\"150.50\"", json);

            var roundTripped = JsonSerializer.Deserialize<decimal>(json, Options);
            Assert.Equal(150.50m, roundTripped);
        }

        [Fact]
        public void Write_UnderAPtBrCulture_UsesAPeriod_NotAComma()
        {
            // This is the converter behind ValorNominal and most other monetary fields; the real
            // serialization bug class this pins is a comma-decimal machine culture silently corrupting the
            // wire format ("10,50" instead of "10.50") in a way the golden-JSON tests never see on en-US CI.
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

                var json = JsonSerializer.Serialize(10.5m, Options);

                Assert.Equal("\"10.50\"", json);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void Read_UnderAPtBrCulture_StillParsesAPeriod_AsTheDecimalSeparator()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

                var value = JsonSerializer.Deserialize<decimal>("\"10.50\"", Options);

                Assert.Equal(10.50m, value);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void Read_AnInvalidDecimal_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal>("\"not-a-number\"", Options));
        }
    }
}
