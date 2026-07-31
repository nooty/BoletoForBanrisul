using BoletoForBanrisul.Core.Converters;
using System.Globalization;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    /// <summary>
    /// Covers <c>BoletoForBanrisulCore/Converters/DecimalF3Converter .cs</c> - note the space before
    /// <c>.cs</c> in that filename. Left alone here: renaming it is out of scope for a test-only change and
    /// would only muddy this commit's diff, but it is worth flagging for cleanup separately.
    /// </summary>
    [Trait("Category", "Unit")]
    public class DecimalF3ConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new DecimalF3Converter() } };

        [Fact]
        public void RoundTrips_AValue_WithThreeDecimalPlaces()
        {
            var json = JsonSerializer.Serialize(16.2534m, Options);
            Assert.Equal("\"16.253\"", json);

            var roundTripped = JsonSerializer.Deserialize<decimal>(json, Options);
            Assert.Equal(16.253m, roundTripped);
        }

        [Fact]
        public void Write_UnderAPtBrCulture_UsesAPeriod_NotAComma()
        {
            // This is the converter behind BeneficiarioRateio.Percentual; the real bug class this pins is a
            // comma-decimal machine culture silently corrupting the wire format ("16,253" instead of
            // "16.253") in a way the golden-JSON tests never see on en-US CI.
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

                var json = JsonSerializer.Serialize(16.253m, Options);

                Assert.Equal("\"16.253\"", json);
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

                var value = JsonSerializer.Deserialize<decimal>("\"16.253\"", Options);

                Assert.Equal(16.253m, value);
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
