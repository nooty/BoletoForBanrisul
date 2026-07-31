using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.IntegrationTests
{
    [Trait("Category", "Integration")]
    public class IssueBoletoTest : AuthenticatedTestBase
    {
        [IntegrationTheory]
        [InlineData("264", null)]
        public async Task Should_Issue_Boleto_Successfully(string id, BoletoIdentifierType? type)
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .EnableLogging()
                .Build();

            var pdf = await client.IssueBoletoAsync(id, type);

            File.WriteAllBytes("issue-boleto-test.pdf", pdf);

            Assert.NotNull(pdf);
        }
    }
}
