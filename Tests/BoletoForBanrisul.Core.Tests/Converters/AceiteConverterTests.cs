using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models.Types;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class AceiteConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new AceiteConverter() } };

        [Theory]
        [InlineData(Aceite.Sim, "A")]
        [InlineData(Aceite.Nao, "N")]
        public void RoundTrips_EveryDefinedValue(Aceite value, string wire)
        {
            var json = JsonSerializer.Serialize(value, Options);
            Assert.Equal($"\"{wire}\"", json);

            var roundTripped = JsonSerializer.Deserialize<Aceite>(json, Options);
            Assert.Equal(value, roundTripped);
        }

        [Fact]
        public void Read_UnknownWireValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Aceite>("\"X\"", Options));
        }

        [Fact]
        public void Write_UndefinedEnumValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Serialize((Aceite)99, Options));
        }
    }
}
