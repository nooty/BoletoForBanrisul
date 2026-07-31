using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models.Types;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class SituacaoCipConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new SituacaoCipConverter() } };

        [Theory]
        [InlineData(SituacaoCip.Aprovado, "A")]
        [InlineData(SituacaoCip.Rejeitado, "R")]
        [InlineData(SituacaoCip.Pendente, "P")]
        [InlineData(SituacaoCip.Enviado, "E")]
        [InlineData(SituacaoCip.NaoCentralizado, "N")]
        public void RoundTrips_EveryDefinedValue(SituacaoCip value, string wire)
        {
            var json = JsonSerializer.Serialize((SituacaoCip?)value, Options);
            Assert.Equal($"\"{wire}\"", json);

            var roundTripped = JsonSerializer.Deserialize<SituacaoCip?>(json, Options);
            Assert.Equal(value, roundTripped);
        }

        [Fact]
        public void RoundTrips_Null()
        {
            var json = JsonSerializer.Serialize((SituacaoCip?)null, Options);
            Assert.Equal("null", json);

            var roundTripped = JsonSerializer.Deserialize<SituacaoCip?>(json, Options);
            Assert.Null(roundTripped);
        }

        [Fact]
        public void Read_UnknownWireValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SituacaoCip?>("\"X\"", Options));
        }
    }
}
