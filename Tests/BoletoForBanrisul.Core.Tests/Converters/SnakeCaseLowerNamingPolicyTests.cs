using BoletoForBanrisul.Core.Converters;

namespace BoletoForBanrisul.Core.Tests.Converters
{
    [Trait("Category", "Unit")]
    public class SnakeCaseLowerNamingPolicyTests
    {
        [Theory]
        [InlineData("NossoNumero", "nosso_numero")]
        [InlineData("Cep", "cep")]
        [InlineData("Uf", "uf")]
        [InlineData("IdTituloEmpresa", "id_titulo_empresa")]
        [InlineData("NotasFiscais", "notas_fiscais")]
        [InlineData("ValorNominal", "valor_nominal")]
        [InlineData("A", "a")]
        [InlineData("", "")]
        public void ConvertName_ConvertsPascalCaseToSnakeCase(string input, string expected)
        {
            Assert.Equal(expected, new SnakeCaseLowerNamingPolicy().ConvertName(input));
        }

        [Fact]
        public void ConvertName_ConsecutiveUppercaseLetters_AreEachSeparatedByAnUnderscore()
        {
            // A real quirk of this policy: it inserts an underscore before *every* uppercase letter
            // regardless of whether the previous character was also uppercase, so an all-caps run like "CEP"
            // becomes "c_e_p" rather than "cep". Not exercised by the model - whose properties use
            // Pascal-with-single-leading-cap names like "Cep" - but worth pinning as documented behaviour of
            // the policy itself, since introducing an ALL_CAPS-style property later would trip over it.
            Assert.Equal("c_e_p", new SnakeCaseLowerNamingPolicy().ConvertName("CEP"));
        }

        [Fact]
        public void ConvertName_Null_ReturnsNull()
        {
            Assert.Null(new SnakeCaseLowerNamingPolicy().ConvertName(null!));
        }
    }
}
