using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models.Types;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class EspecieConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new EspecieConverter() } };

        [Theory]
        [InlineData(Especie.DuplicataMercantil, "02")]
        [InlineData(Especie.DuplicataServico, "04")]
        [InlineData(Especie.CartaoCredito, "31")]
        [InlineData(Especie.BoletoProposta, "32")]
        [InlineData(Especie.Outros, "99")]
        public void RoundTrips_EveryDefinedValue_AsATwoDigitCode(Especie value, string wire)
        {
            var json = JsonSerializer.Serialize(value, Options);
            Assert.Equal($"\"{wire}\"", json);

            var roundTripped = JsonSerializer.Deserialize<Especie>(json, Options);
            Assert.Equal(value, roundTripped);
        }

        [Fact]
        public void Read_UnknownWireValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Especie>("\"77\"", Options));
        }

        [Fact]
        public void Read_NonNumericWireValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Especie>("\"XX\"", Options));
        }
    }
}
