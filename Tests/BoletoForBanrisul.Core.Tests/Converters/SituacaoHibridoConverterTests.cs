using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models.Types;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class SituacaoHibridoConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new SituacaoHibridoConverter() } };

        [Theory]
        [InlineData(SituacaoHibrido.Criado, "A")]
        [InlineData(SituacaoHibrido.NaoCriado, "N")]
        [InlineData(SituacaoHibrido.Pendente, "P")]
        public void RoundTrips_EveryDefinedValue(SituacaoHibrido value, string wire)
        {
            var json = JsonSerializer.Serialize(value, Options);
            Assert.Equal($"\"{wire}\"", json);

            var roundTripped = JsonSerializer.Deserialize<SituacaoHibrido>(json, Options);
            Assert.Equal(value, roundTripped);
        }

        [Fact]
        public void Read_UnknownWireValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SituacaoHibrido>("\"X\"", Options));
        }

        [Fact]
        public void Write_UndefinedEnumValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Serialize((SituacaoHibrido)99, Options));
        }
    }
}
