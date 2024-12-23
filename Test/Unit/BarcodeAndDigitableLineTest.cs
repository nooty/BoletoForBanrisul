using Xunit;
using System;
using BoletoForBanrisul.Core.Helpers;
using BoletoForBanrisul.Core.Models;

namespace Test
{
    [Trait("Category", "Unit")]
    public class BarcodeAndDigitableLineTest
    {
        [Theory]
        [InlineData(2021, 05, 05, 150.50, "0010000001088", "0006445268", "04198861100000150502100100000010000644524040")]
        [InlineData(2021, 05, 05, 150.50, "0010000001088", "0000038806", "04197861100000150502100100000010000003884071")]
        [InlineData(2022, 12, 29, 999.99, "0010000001088", "5945952743", "04191921400000999992100100000010594595274022")]
        [InlineData(2019, 09, 30, 8000.00, "0010000001088", "0000007781", "04196802800008000002100100000010000000774062")]
        [InlineData(2019, 09, 30, 8000.00, "X10X0000X0108X8X", "XX7XX7XXXXX", "04196802800008000002100100000010000000774062")]
        [InlineData(2021, 05, 05, 150.50, "0010000001088", "0006445675", "04193861100000150502100100000010000644564050")]
        [InlineData(2021, 05, 05, 150.50, "10000001088", "00064456", "04193861100000150502100100000010000644564050")]
        [InlineData(2021, 05, 05, 150.50, "10000001088", "0006445438", "04191861100000150502100100000010000644544009")]
        public void Should_Generate_Valid_CodigoDeBarras_From_Boleto(int y, int m, int d, decimal v, string b, string n, 
            string result)
        {
            // Arrange
            var boleto = new Boleto
            {
                Titulo = new Titulo
                {
                    DataVencimento = new DateTime(y, m, d),
                    ValorNominal = v,
                    Beneficiario = new Beneficiario { Codigo = b },
                    NossoNumero = n
                }
            };

            // Act
            string codigoBarras = BoletoHelper.GetBarcode(boleto);

            // Assert
            Assert.Equal(result, codigoBarras);
        }

        [Theory]
        [InlineData(2021, 05, 05, 150.50, "0010000001088", "0006445268", "04192100180000001000906445240408886110000015050")]
        [InlineData(2021, 05, 05, 150.50, "0010000001088", "0000038806", "04192100180000001000900038840716786110000015050")]
        [InlineData(2022, 12, 29, 999.99, "0010000001088", "5945952743", "04192100180000001059545952740228192140000099999")]
        [InlineData(2019, 09, 30, 8000.00, "0010000001088", "0000007781", "04192100180000001000900007740624680280000800000")]
        [InlineData(2019, 09, 30, 8000.00, "10000001088", "7781", "04192100180000001000900778140673580280000800000")]
        [InlineData(2021, 05, 05, 150.50, "0010000001088", "0006445675", "04192100180000001000906445640508386110000015050")]
        [InlineData(2021, 05, 05, 150.50, "10000001088", "6445675", "04192100180000001006644567540883186110000015050")]
        [InlineData(2021, 05, 05, 150.50, "10000001088", "0006445438", "04192100180000001000906445440099186110000015050")]
        public void Should_Generate_Valid_LinhaDigitavel_From_Boleto(int y, int m, int d, decimal v, string b, string n,
            string result)
        {
            // Arrange
            var boleto = new Boleto
            {
                Titulo = new Titulo
                {
                    DataVencimento = new DateTime(y, m, d),
                    ValorNominal = v,
                    Beneficiario = new Beneficiario { Codigo = b },
                    NossoNumero = n
                }
            };

            // Act
            string codigoBarras = BoletoHelper.GetDigitableLine(boleto);

            // Assert
            Assert.Equal(result, codigoBarras);
        }

        [Theory]
        [InlineData("04192100180000001000906445240408886110000015050", "04192.10018  00000.010009  06445.240408  8  86110000015050")]
        [InlineData("04192100180000001000900038840716786110000015050", "04192.10018  00000.010009  00038.840716  7  86110000015050")]
        [InlineData("04192100180000001059545952740228192140000099999", "04192.10018  00000.010595  45952.740228  1  92140000099999")]
        [InlineData("04192100180000001000900007740624680280000800000", "04192.10018  00000.010009  00007.740624  6  80280000800000")]
        [InlineData("04192100180000001000906445640508386110000015050", "04192.10018  00000.010009  06445.640508  3  86110000015050")]
        [InlineData("04192100180000001000906445440099186110000015050", "04192.10018  00000.010009  06445.440099  1  86110000015050")]
        public void Should_Format_LinhaDigitavel_Correctly(string linhaDigitavel, string expectedFormatted)
        {
            // Act
            string formattedLinhaDigitavel = BoletoHelper.FormatDigitableLine(linhaDigitavel);

            // Assert
            Assert.Equal(expectedFormatted, formattedLinhaDigitavel);
        }
    }

}
