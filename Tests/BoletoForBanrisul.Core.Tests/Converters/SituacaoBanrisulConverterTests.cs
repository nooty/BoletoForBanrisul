using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models.Types;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class SituacaoBanrisulConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new SituacaoBanrisulConverter() } };

        [Theory]
        [InlineData(SituacaoBanrisul.Ativo, "A")]
        [InlineData(SituacaoBanrisul.BaixadoPorPagamento, "B")]
        [InlineData(SituacaoBanrisul.BaixadoPorDevolucao, "D")]
        [InlineData(SituacaoBanrisul.Liquidado, "L")]
        [InlineData(SituacaoBanrisul.Reembolsado, "R")]
        [InlineData(SituacaoBanrisul.TransferidoParaCL, "T")]
        [InlineData(SituacaoBanrisul.Protestado, "P")]
        public void RoundTrips_EveryDefinedValue(SituacaoBanrisul value, string wire)
        {
            var json = JsonSerializer.Serialize((SituacaoBanrisul?)value, Options);
            Assert.Equal($"\"{wire}\"", json);

            var roundTripped = JsonSerializer.Deserialize<SituacaoBanrisul?>(json, Options);
            Assert.Equal(value, roundTripped);
        }

        [Fact]
        public void RoundTrips_Null()
        {
            var json = JsonSerializer.Serialize((SituacaoBanrisul?)null, Options);
            Assert.Equal("null", json);

            var roundTripped = JsonSerializer.Deserialize<SituacaoBanrisul?>(json, Options);
            Assert.Null(roundTripped);
        }

        [Fact]
        public void Read_UnknownWireValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SituacaoBanrisul?>("\"X\"", Options));
        }
    }
}
