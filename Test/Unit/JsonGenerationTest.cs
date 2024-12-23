using BoletoForBanrisul.Core.Models;
using System.Text.Json;

namespace Test
{
    [Trait("Category", "Unit")]
    public class JsonGenerationTest
    {
        [Fact]
        public void Should_Generate_JSON_From_Boleto1_And_Match_Original()
        {
            Boleto boleto = BoletoGenerator.GenerateBoleto1();
            string jsonOriginal = File.ReadAllText("Resources/Boleto1.json");

            var json = boleto.ToJson();

            Assert.Equal(jsonOriginal, json);
        }

        [Fact]
        public void Should_Generate_JSON_From_Boleto2_And_Match_Original()
        {
            Boleto boleto = BoletoGenerator.GenerateBoleto2();
            string jsonOriginal = File.ReadAllText("Resources/Boleto2.json");

            var json = boleto.ToJson();

            Assert.Equal(jsonOriginal, json);
        }
    }
}