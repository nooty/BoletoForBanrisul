using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoletoForBanrisul.Core.Helpers;
using Xunit;

namespace Test
{
    [Trait("Category", "Unit")]
    public class OurNumberHelperTest
    {
        [Theory]
        [InlineData("00189274", "0018927446")]
        [InlineData("001892749999999", "0018927446")]
        [InlineData("00189274XXXXXX", "0018927446")]
        [InlineData("XX00X18X9274XXXXXX", "0018927446")]
        [InlineData("59458645", "5945864550")]
        [InlineData("20026262", "2002626244")]
        [InlineData("20013302", "2001330236")]
        [InlineData("189274", "0018927446")]
        [InlineData("99017254", "9901725478")]
        public void Should_Return_Correct_NossoNumero_When_Valid_Input(string ourNumeroBase, string fullOurNumber)
        {
            // Act
            string result = BoletoHelper.GetOurNumberWithCheckDigits(ourNumeroBase);

            // Assert
            Assert.Equal(fullOurNumber, result);
        }

        [Fact]
        public void Should_Pad_Zeroes_When_Input_Has_Less_Than_8_Digits()
        {
            // Arrange
            string ourNumeroBase = "189"; 

            // Act
            string result = BoletoHelper.GetOurNumberWithCheckDigits(ourNumeroBase);

            // Assert
            Assert.Equal("0000018910", result); // Completado com zeros
        }

        [Fact]
        public void Should_Handle_Non_Numeric_Characters()
        {
            // Arrange
            string ourNumeroBase = "18a9b27c4"; // Contém caracteres não numéricos

            // Act
            string result = BoletoHelper.GetOurNumberWithCheckDigits(ourNumeroBase);

            // Assert
            Assert.Equal("0018927446", result); // Deve ignorar os caracteres não numéricos
        }

        [Fact]
        public void Should_Return_Correct_OurNumber_When_Input_Has_Exactly_8_Digits()
        {
            // Arrange
            string ourNumberBase = "18927412";

            // Act
            string result = BoletoHelper.GetOurNumberWithCheckDigits(ourNumberBase);

            // Assert
            Assert.Equal("1892741294", result); 
        }
    }

}
