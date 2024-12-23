using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoletoForBanrisul.Client.Exceptions.Api;
using BoletoForBanrisul.Client.Exceptions.Auth;
using BoletoForBanrisul.Client.Models.Responses;

namespace Test
{
    [Trait("Category", "Integration")]
    public class BoletoGrantAbatementTest : AuthenticatedTestBase
    {
        [IntegrationTheory]
        [InlineData("264", null)]
        [InlineData("04192100180000001000900000240473186110000015050", BoletoIdentifierType.DigitableLine)]
        [InlineData("04191861100000150502100100000010000000024047", BoletoIdentifierType.Barcode)]
        public async Task Should_Cancel_Boleto_Successfully(string idBoleto, BoletoIdentifierType? type)
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .EnableLogging()
                .Build();

            var response = await client.GrantAbatementAsync(idBoleto, 10, type);

            Assert.Equal(ResponseCode.TestApproved, response.Retorno);
            Assert.NotNull(response.Titulo);
            Assert.Equal("0000000264", response.Titulo.NossoNumero);
        }

        [IntegrationFact]
        public async Task Should_Throw_UnauthorizedException_For_Unauthorized_Beneficiary()
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0001000000252")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            await Assert.ThrowsAsync<UnauthorizedException>(async () =>
            {
                var response = await client.GrantAbatementAsync("264", 10);
            });
        }

        [IntegrationFact]
        public async Task Should_Throw_NotFoundException_For_NonExistent_Boleto()
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                var response = await client.GrantAbatementAsync("1090", 10);
            });
        }

        [IntegrationFact]
        public async Task Should_Throw_NotFoundException_For_NonExistent_Beneficiary()
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0001000000414")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                var response = await client.GrantAbatementAsync("264", 10);
            });
        }
    }
}
