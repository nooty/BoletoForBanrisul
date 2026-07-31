using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models.Types;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class TipoPessoaConverterTests
    {
        private static readonly JsonSerializerOptions Options = new() { Converters = { new TipoPessoaConverter() } };

        [Theory]
        [InlineData(TipoPessoa.PessoaFisica, "F")]
        [InlineData(TipoPessoa.PessoaJuridica, "J")]
        public void RoundTrips_EveryDefinedValue(TipoPessoa value, string wire)
        {
            var json = JsonSerializer.Serialize(value, Options);
            Assert.Equal($"\"{wire}\"", json);

            var roundTripped = JsonSerializer.Deserialize<TipoPessoa>(json, Options);
            Assert.Equal(value, roundTripped);
        }

        [Fact]
        public void Read_UnknownWireValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TipoPessoa>("\"X\"", Options));
        }

        [Fact]
        public void Write_UndefinedEnumValue_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Serialize((TipoPessoa)99, Options));
        }
    }
}
