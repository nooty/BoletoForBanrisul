using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;

namespace BoletoForBanrisul.Core.Tests
{
    /// <summary>Builders for the bank slips these tests work from.</summary>
    /// <remarks>
    /// Mirrors <c>BoletoForSantander.Core.Tests.BoletoFactory</c>, but builds a <see cref="Titulo"/> rather
    /// than a full <see cref="Boleto"/>: <see cref="Titulo"/> is where every domain property lives (the
    /// outer <see cref="Boleto"/> only adds <see cref="Boleto.Ambiente"/>), and validating a <see cref="Titulo"/>
    /// directly keeps error paths unprefixed (<c>"Pagador.Cep"</c>) the same way Santander's own tests read.
    /// Tests that specifically need the one extra level of nesting (the "Titulo." prefix a real request
    /// carries) wrap the result in a <see cref="Boleto"/> themselves.
    /// </remarks>
    internal static class BoletoFactory
    {
        /// <summary>The payer's CPF from the bank's own sandbox samples ("Homologado com nosso número" and others). Passes the check-digit algorithm.</summary>
        internal const string ValidCpf = "00000000191";

        /// <summary>The sacador's CPF from the bank's "Com Sacador" sample. Passes the check-digit algorithm.</summary>
        internal const string ValidSacadorCpf = "00000000272";

        /// <summary>A CNPJ from the bank's "Com Rateio" sample (the sharing beneficiaries). Passes the check-digit algorithm.</summary>
        internal const string ValidCnpj = "92702067000196";

        /// <summary>The Federal Revenue's official alphanumeric CNPJ example, including its check digits.</summary>
        internal const string ValidAlphanumericCnpj = "12ABC34501DE35";

        /// <summary>Well-formed at 11 digits (repeated), so it satisfies every attribute but fails the check-digit algorithm - the natural negative case.</summary>
        internal const string PlaceholderCpf = "11111111111";

        internal static Pagador CreateValidPagador() => new Pagador
        {
            TipoPessoa = TipoPessoa.PessoaFisica,
            CpfCnpj = ValidCpf,
            Nome = "PAGADOR FICTICIO",
            Endereco = "RUA CALDAS JUNIOR 120",
            Cep = "90010260",
            Cidade = "PORTO ALEGRE",
            Uf = "RS",
            Aceite = Aceite.Sim
        };

        /// <summary>
        /// The minimum valid bank slip: only the properties the bank's own schema requires, plus
        /// <see cref="Titulo.Especie"/> for clarity, with <see cref="Titulo.Instrucoes"/> and
        /// <see cref="Titulo.PagParcial"/> left at their own (valid) defaults - see the <c>&lt;remarks&gt;</c>
        /// on <see cref="Titulo"/> itself for why those defaults have to stay valid.
        /// </summary>
        internal static Titulo CreateMinimal() => new Titulo
        {
            SeuNumero = "64452-1",
            DataVencimento = new DateTime(2026, 8, 30),
            DataEmissao = new DateTime(2026, 7, 29),
            ValorNominal = 150.50m,
            Especie = Especie.Outros,
            Pagador = CreateValidPagador()
        };

        /// <summary>
        /// A bank slip with every optional block populated and every validation rule satisfied: used both as
        /// the baseline that individual faults get injected into, and to prove the fully-populated shape is
        /// itself accepted.
        /// </summary>
        internal static Titulo CreateValid() => new Titulo
        {
            NossoNumero = "0000038806",
            SeuNumero = "64452-1",
            DataVencimento = new DateTime(2026, 8, 30),
            ValorNominal = 150.50m,
            Especie = Especie.Outros,
            DataEmissao = new DateTime(2026, 7, 29),
            IdTituloEmpresa = "0011000000000000000064452",
            Pagador = CreateValidPagador(),
            Sacador = new Sacador
            {
                TipoPessoa = TipoPessoa.PessoaFisica,
                CpfCnpj = ValidSacadorCpf,
                Nome = "SACADOR FICTICIO",
                Endereco = "RUA CALDAS JUNIOR 120",
                Cep = "90010260"
            },
            Instrucoes = new Instrucoes
            {
                Juros = new Juros { Codigo = CodigoJuros.TaxaMensal, Taxa = 1.00m },
                Multa = new Multa { Codigo = CodigoMulta.Percentual, Data = new DateTime(2026, 9, 2), Taxa = 0.4m },
                Desconto = new Desconto { Codigo = CodigoDesconto.ValorFixoAteData, Data = new DateTime(2026, 8, 15), Valor = 6.95m },
                Abatimento = new Abatimento { Valor = 6.80m },
                Protesto = new Protesto { Codigo = CodigoProtesto.NegativarDiasCorridos, Prazo = 5 },
                Baixa = new Baixa { Codigo = CodigoBaixa.BaixarDevolver, Prazo = 30 }
            },
            PagParcial = new PagamentoParcial
            {
                Autoriza = AutorizacaoPagamentoParcial.NaoAutoriza,
                Codigo = CodigoPagamentoParcial.ValorEntreMinimoEMaximo,
                Quantidade = 3,
                Tipo = TipoValor.Valor,
                ValorMin = 10.00m,
                ValorMax = 90.00m
            },
            Mensagens = new List<Mensagem>
            {
                new Mensagem { Linha = 1, Texto = "PAGAMENTO PREFERENCIALMENTE NO BANRISUL" },
                new Mensagem { Linha = 2, Texto = "NAO ACEITAR PAGAMENTO COM CHEQUE" }
            },
            // Note: values sum to exactly ValorNominal (150.50) so the "sum must not exceed ValorNominal"
            // rule (Titulo.ValidateSharingValueAgainstNominalValue) stays quiet.
            Rateio = new Rateio
            {
                Codigo = CodigoRateio.ValorRegistradoTitulo,
                TipoValor = TipoValor.Valor,
                Beneficiarios = new List<BeneficiarioRateio>
                {
                    new BeneficiarioRateio
                    {
                        Codigo = "0010000001088",
                        Valor = 100.00m,
                        TipoPessoa = TipoPessoa.PessoaJuridica,
                        CpfCnpj = ValidCnpj,
                        Nome = "BANRISUL COBRANCA-TESTE",
                        NomeFantasia = "BANRISUL"
                    },
                    new BeneficiarioRateio
                    {
                        Codigo = "0010022145083",
                        Valor = 50.50m,
                        TipoPessoa = TipoPessoa.PessoaJuridica,
                        CpfCnpj = ValidCnpj,
                        Nome = "BANRISUL CARTEIRA TESTE",
                        NomeFantasia = "BANRISUL"
                    }
                }
            },
            // PagParcial.Autoriza stays NaoAutoriza above, so rule 10 (Hibrido set requires NaoAutoriza) is
            // satisfied regardless of which way Hibrido.Autoriza itself points.
            Hibrido = new Hibrido { Autoriza = SimNao.Nao },
            NotasFiscais = new List<NotaFiscal>
            {
                new NotaFiscal { Danfe = "11111111111111111111111111111111111111111112" },
                new NotaFiscal { Danfe = "11111111111111111111111111111111111111111120" }
            }
        };
    }
}
