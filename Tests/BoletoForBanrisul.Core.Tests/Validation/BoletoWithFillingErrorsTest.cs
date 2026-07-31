using BoletoForBanrisul.Core.Validation;

namespace BoletoForBanrisul.Core.Tests.Validation
{
    /// <summary>
    /// Replaces the old <c>Test/Integration/BoletoCreateTest.Should_Throw_InvalidRequestException_For_Boleto_With_Filling_Errors</c>.
    /// That test sent a malformed <see cref="Models.Boleto"/> and asserted the bank answered with a 400. Local
    /// validation now runs as the first statement of <c>BoletoClient.CreateBoletoAsync</c>, so a malformed
    /// boleto never reaches the bank at all - the request the old test relied on can no longer happen. This
    /// asserts the same "malformed boleto is rejected" intent directly against
    /// <see cref="RecursiveValidator"/>, the layer that now owns the rejection.
    /// </summary>
    [Trait("Category", "Unit")]
    public class BoletoWithFillingErrorsTest
    {
        [Fact]
        public void Should_Throw_ModelValidationException_For_Boleto_With_Filling_Errors()
        {
            var boleto = BoletoGenerator.GenerateBoletoWithFillingErrors();

            var exception = Assert.Throws<ModelValidationException>(() => RecursiveValidator.ValidateAndThrow(boleto));

            var paths = exception.Errors
                .Select(e => e.MemberNames.FirstOrDefault())
                .ToList();

            Assert.Contains("Titulo.SeuNumero", paths);
            Assert.Contains("Titulo.DataVencimento", paths);
            Assert.Contains("Titulo.DataEmissao", paths);
            Assert.Contains(paths, path => path is not null && path.StartsWith("Titulo.Pagador.", StringComparison.Ordinal));
        }
    }
}
