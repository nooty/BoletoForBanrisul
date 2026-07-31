using BoletoForBanrisul.Core.Converters;
using System.Globalization;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class DecimalF1ConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new DecimalF1Converter() } };

        [Fact]
        public void RoundTrips_AValue_WithOneDecimalPlace()
        {
            var json = JsonSerializer.Serialize(10.5m, Options);
            Assert.Equal("\"10.5\"", json);

            var roundTripped = JsonSerializer.Deserialize<decimal>(json, Options);
            Assert.Equal(10.5m, roundTripped);
        }

        [Fact]
        public void Write_UnderAPtBrCulture_UsesAPeriod_NotAComma()
        {
            // BoletoBounds.Rate4Max-scale values (Multa.Taxa) round-trip through this converter; if Write ever
            // dropped the explicit InvariantCulture, a comma-decimal machine culture would corrupt the wire
            // format silently - the golden-JSON tests would not catch this on an en-US CI machine.
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

                var json = JsonSerializer.Serialize(10.5m, Options);

                Assert.Equal("\"10.5\"", json);
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

                var value = JsonSerializer.Deserialize<decimal>("\"10.5\"", Options);

                Assert.Equal(10.5m, value);
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
