using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models.Types;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class SimNaoConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new SimNaoConverter() } };

        [Theory]
        [InlineData(SimNao.Sim, "S")]
        [InlineData(SimNao.Nao, "N")]
        public void RoundTrips_EveryDefinedValue(SimNao value, string wire)
        {
            var json = JsonSerializer.Serialize(value, Options);
            Assert.Equal($"\"{wire}\"", json);

            var roundTripped = JsonSerializer.Deserialize<SimNao>(json, Options);
            Assert.Equal(value, roundTripped);
        }

        [Fact]
        public void Read_UnknownWireValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SimNao>("\"X\"", Options));
        }

        [Fact]
        public void Write_UndefinedEnumValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Serialize((SimNao)99, Options));
        }
    }
}
