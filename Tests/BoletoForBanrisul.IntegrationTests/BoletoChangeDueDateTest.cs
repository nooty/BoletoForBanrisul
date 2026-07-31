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

namespace BoletoForBanrisul.IntegrationTests
{
    [Trait("Category", "Integration")]
    public class BoletoChangeDueDateTest : AuthenticatedTestBase
    {
        [IntegrationFact]
        public async Task Should_Change_Due_Date_Successfully()
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .EnableLogging()
                .Build();

            var dueDate = new DateTime(2024, 12, 31);

            var response = await client.ChangeDueDateAsync("264", dueDate);

            Assert.Equal(ResponseCode.TestApproved, response.Retorno);
            Assert.NotNull(response.Titulo);
            Assert.Equal(dueDate, response.Titulo.DataVencimento);
        }

        [IntegrationFact]
        public async Task Should_Throw_UnauthorizedException_For_Unauthorized_Beneficiary()
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0001000000252")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var dueDate = new DateTime(2024, 12, 31);

            await Assert.ThrowsAsync<UnauthorizedException>(async () =>
            {
                var response = await client.ChangeDueDateAsync("264", dueDate);
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

            var dueDate = new DateTime(2024, 12, 31);

            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                var response = await client.ChangeDueDateAsync("1090", dueDate);
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

            var dueDate = new DateTime(2024, 12, 31);

            await Assert.ThrowsAsync<NotFoundException>(async () =>
            {
                var response = await client.ChangeDueDateAsync("264", dueDate);
            });
        }
    }
}
