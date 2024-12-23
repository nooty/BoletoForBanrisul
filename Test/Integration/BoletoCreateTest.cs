using BoletoForBanrisul.Client;
using BoletoForBanrisul.Client.Exceptions.Api;
using BoletoForBanrisul.Client.Exceptions.Auth;
using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Client.Models.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace Test
{
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

        [IntegrationFact]
        public async Task Should_Throw_InvalidRequestException_For_Boleto_With_Filling_Errors()
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var boleto = BoletoGenerator.GenerateBoletoWithFillingErrors();

            var ex = await Assert.ThrowsAsync<InvalidRequestException>(async () =>
            {
                var response = await client.CreateBoletoAsync(boleto);
            });

            Assert.NotEmpty(ex.Ocurrences);
        }
    }
}
