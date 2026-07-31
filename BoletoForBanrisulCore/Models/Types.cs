using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Core.Models.Types
{
    public enum Ambiente
    {
        Producao,
        Teste
    }

    public enum TipoPessoa
    {
        PessoaFisica,
        PessoaJuridica
    }

    public enum Especie
    {
        DuplicataMercantil = 02,
        DuplicataServico = 04,
        CartaoCredito = 31,
        BoletoProposta = 32,
        Outros = 99
    }

    public enum AutorizacaoPagamentoParcial
    {
        NaoAutoriza = 1,
        Autoriza = 2
    }

    public enum CodigoPagamentoParcial
    {
        QualquerValor = 1,
        ValorEntreMinimoEMaximo = 2,
        NaoAceitaDivergente = 3
    }

    public enum TipoValor
    {
        Percentual = 1,
        Valor = 2
    }

    public enum CodigoJuros
    {
        ValorPorDia = 1,
        TaxaMensal = 2,
        Isento = 3
    }

    public enum CodigoMulta
    {
        ValorFixo = 1,
        Percentual = 2
    }

    public enum CodigoDesconto
    {
        ValorFixoAteData = 1,
        PercentualAteData = 2,
        ValorPorAntecipacaoDiaCorrido = 3,
        PercentualSobreValorNominalDiaCorrido = 5
    }

    public enum CodigoProtesto
    {
        /// <summary>Protest after a number of calendar days past due.</summary>
        ProtestarDiasCorridos = 1,

        /// <summary>Do not protest.</summary>
        NaoProtestar = 3,

        /// <summary>Do not report the payer to credit bureaus ("negativação").</summary>
        NaoNegativar = 7,

        /// <summary>Report the payer to credit bureaus ("negativação") after a number of calendar days past due.</summary>
        NegativarDiasCorridos = 8
    }

    public enum CodigoBaixa
    {
        BaixarDevolver = 1
    }

    public enum CodigoRateio
    {
        ValorCobradoPagador = 1,
        ValorRegistradoTitulo = 2
    }

    public enum SimNao
    {
        Sim,
        Nao
    }

    public enum Aceite
    {
        Sim,
        Nao
    }

    public enum SituacaoHibrido
    {
        Criado,
        NaoCriado,
        Pendente
    }

    public enum SituacaoBanrisul
    {
        Ativo,
        BaixadoPorPagamento,
        BaixadoPorDevolucao,
        Liquidado,
        Reembolsado,
        TransferidoParaCL,
        Protestado
    }

    public enum SituacaoCip
    {
        Aprovado,
        Rejeitado,
        Pendente,
        Enviado,
        NaoCentralizado
    }

    public enum SituacaoPagamento
    {
        AptoParaPagamento = 1,
        PagamentoEmProcessamento = 2,
        CreditoRetido = 3,
        BaixadoNoDia = 4,
        EmProcessoDeDevolucaoAutomatica = 5,
        EmCartorio = 6,
        PagamentoIndisponivelEntreEmContato = 7
    }

    public enum Carteira
    {
        CobrancaSimples = 1,
        CobrancaVinculada = 2,
        CobrancaCaucionada = 3,
        Desconto = 4,
        Vendor = 5
    }
}
