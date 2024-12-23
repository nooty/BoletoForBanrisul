using BoletoForBanrisul.Client;
using BoletoForBanrisul.Client.Exceptions.Api;
using BoletoForBanrisul.Client.Exceptions.Auth;
using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Client.Models.Responses;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace Test
{
    [Trait("Category", "Integration")]
    public class BoletoGetTest : AuthenticatedTestBase
    {
        [IntegrationTheory]
        [InlineData("6445268", null)]
        [InlineData("6445268", BoletoIdentifierType.OurNumber)]
        [InlineData("04191861100000150502100100000010000000024047", BoletoIdentifierType.Barcode)]
        [InlineData("04192100180000001000906445240408886110000015050", BoletoIdentifierType.DigitableLine)]
        public async Task Should_Get_Boleto_Successfully(string id, BoletoIdentifierType? type)
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var response = await client.GetBoletoAsync(id, type);

            Assert.Equal(ResponseCode.SuccessAndCentralized, response.Retorno);
            Assert.Equal(SituacaoPagamento.AptoParaPagamento, response.Titulo?.SituacaoPagamento);
        }

        [IntegrationTheory]
        [InlineData("5885491547", null)]
        public async Task Should_Get_Canceled_Boleto_Successfully(string id, BoletoIdentifierType? type)
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .EnableLogging()
                .Build();

            var response = await client.GetBoletoAsync(id, type);

            Assert.Equal(ResponseCode.SuccessAndCentralized, response.Retorno);
            Assert.Equal(new DateTime(2020, 01, 10), response.Titulo?.Operacoes?.DataBaixa);
        }

        [IntegrationTheory]
        [InlineData("5945863406", null)]
        public async Task Should_Get_Boleto_With_Pending_Pix_Code_Successfully(string id, BoletoIdentifierType? type)
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var response = await client.GetBoletoAsync(id, type);

            Assert.Equal(ResponseCode.SuccessAndCentralized, response.Retorno);
            Assert.Equal(SituacaoHibrido.Pendente, response.Titulo?.Hibrido?.Situacao);
        }

        [IntegrationTheory]
        [InlineData("5942970953", null)]
        public async Task Should_Get_Returned_Boleto_Successfully(string id, BoletoIdentifierType? type)
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var response = await client.GetBoletoAsync(id, type);

            Assert.Equal(ResponseCode.SuccessAndCentralized, response.Retorno);
            Assert.Equal(new DateTime(2021, 10, 14), response.Titulo?.Operacoes?.DataBaixa);
            Assert.Equal(SituacaoBanrisul.BaixadoPorDevolucao, response.Titulo?.SituacaoBanrisul);
        }

        [IntegrationTheory]
        [InlineData("6445438", null)]
        public async Task Should_Get_Boleto_With_Fine_And_Abatement_Successfully(string id, BoletoIdentifierType? type)
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var response = await client.GetBoletoAsync(id, type);

            Assert.Equal(ResponseCode.SuccessAndCentralized, response.Retorno);
            Assert.NotNull(response.Titulo?.Instrucoes?.Multa);
            Assert.NotNull(response.Titulo?.Instrucoes?.Abatimento);

            Assert.Equal(CodigoMulta.Percentual, response.Titulo?.Instrucoes?.Multa.Codigo);  
            Assert.Equal(new DateTime(2021, 05, 06), response.Titulo?.Instrucoes?.Multa.Data); 
            Assert.Equal(0.4m, response.Titulo?.Instrucoes?.Multa.Taxa);  

            Assert.Equal(6.80m, response.Titulo?.Instrucoes?.Abatimento.Valor);  
        }

        [IntegrationTheory]
        [InlineData("7781", null)]
        public async Task Should_Get_Settled_Boleto_Successfully(string id, BoletoIdentifierType? type)
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var response = await client.GetBoletoAsync(id, type);

            Assert.Equal(ResponseCode.SuccessAndCentralized, response.Retorno);
            Assert.Equal(SituacaoBanrisul.Liquidado, response.Titulo?.SituacaoBanrisul);
            Assert.Equal(SituacaoCip.Aprovado, response.Titulo?.SituacaoCip);
            Assert.Equal(Carteira.CobrancaSimples, response.Titulo?.Carteira);

            Assert.Equal(new DateTime(2019, 10, 01), response.Titulo?.Operacoes?.DataPagamento);
            Assert.Equal(new DateTime(2019, 10, 02), response.Titulo?.Operacoes?.DataCredito);
            Assert.Equal(10.00m, response.Titulo?.Operacoes?.ValorJurosRecebido);
            Assert.Equal(8000.00m, response.Titulo?.Operacoes?.ValorPagamento);
            Assert.Equal(8010.00m, response.Titulo?.Operacoes?.ValorCobrado);
            Assert.Equal(8010.00m, response.Titulo?.Operacoes?.ValorCreditadoDebitado);
        }

        [IntegrationTheory]
        [InlineData("5945744857", null)]
        public async Task Should_Get_Boleto_Rejected_By_CIP_Successfully(string id, BoletoIdentifierType? type)
        {
            var client = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var response = await client.GetBoletoAsync(id, type);

            Assert.Equal(ResponseCode.Success, response.Retorno);

            Assert.Equal(SituacaoBanrisul.Ativo, response.Titulo?.SituacaoBanrisul);
            Assert.Equal(SituacaoCip.Rejeitado, response.Titulo?.SituacaoCip);
            Assert.Equal(Carteira.CobrancaSimples, response.Titulo?.Carteira);
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
                var response = await client.GetBoletoAsync("6445268");
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
                var response = await client.GetBoletoAsync("1090");
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
                var response = await client.GetBoletoAsync("6445268");
            });
        }
    }
}
