using BoletoForBanrisul.Client;
using BoletoForBanrisul.Client.Exceptions.Auth;
using BoletoForBanrisul.Client.Models.Responses;

namespace BoletoForBanrisul.IntegrationTests
{
    /// <summary>
    /// A third test used to live here, <c>Should_Throw_InvalidRequestException_For_Boleto_With_Filling_Errors</c>:
    /// it sent a malformed <see cref="Core.Models.Boleto"/> and asserted the bank answered with a 400. Local
    /// validation (<see cref="Core.Validation.RecursiveValidator"/>, wired into
    /// <c>BoletoClient.CreateBoletoAsync</c> as its first statement) now rejects that same malformed boleto
    /// before any HTTP request is built, so the test became impossible by design - there is no request left
    /// for the bank to reject. It was rewritten as a unit test against <c>RecursiveValidator</c> directly: see
    /// <c>BoletoWithFillingErrorsTest</c> in <c>Core.Tests/Validation</c>.
    /// </summary>
    [Trait("Category", "Integration")]
    public class BoletoCreateTest : AuthenticatedTestBase
    {
        [IntegrationFact]
        public async Task Should_Create_Boleto_Successfully()
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .EnableLogging()
                .Build();

            var boleto = BoletoGenerator.GenerateBoleto2();
            boleto.Titulo.LinhaDigitavel = "04191861100000150502100100000010000000024047";

            var response = await client.CreateBoletoAsync(boleto);

            Assert.Equal(ResponseCode.TestApproved, response.Retorno);
        }

        [IntegrationFact]
        public async Task Should_Throw_UnauthorizedException_For_Unauthorized_Beneficiary()
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0001000000252")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var boleto = BoletoGenerator.GenerateBoleto2();

            await Assert.ThrowsAsync<UnauthorizedException>(async () =>
            {
                var response = await client.CreateBoletoAsync(boleto);
            });
        }
    }
}
