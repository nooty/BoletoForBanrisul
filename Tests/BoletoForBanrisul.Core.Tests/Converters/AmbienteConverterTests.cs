using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models.Types;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class AmbienteConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new AmbienteConverter() } };

        [Theory]
        [InlineData(Ambiente.Producao, "P")]
        [InlineData(Ambiente.Teste, "T")]
        public void RoundTrips_EveryDefinedValue(Ambiente value, string wire)
        {
            var json = JsonSerializer.Serialize(value, Options);
            Assert.Equal($"\"{wire}\"", json);

            var roundTripped = JsonSerializer.Deserialize<Ambiente>(json, Options);
            Assert.Equal(value, roundTripped);
        }

        [Fact]
        public void Read_UnknownWireValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Ambiente>("\"X\"", Options));
        }

        [Fact]
        public void Write_UndefinedEnumValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Serialize((Ambiente)99, Options));
        }
    }
}
