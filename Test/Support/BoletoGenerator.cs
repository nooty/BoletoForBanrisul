using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class BoletoGenerator
    {
        public static Boleto GenerateBoleto1()
        {
            return new Boleto
            {
                Ambiente = Ambiente.Teste,
                Titulo = new Titulo
                {
                    NossoNumero = "264",
                    SeuNumero = "64452-A",
                    DataVencimento = new DateTime(2021, 01, 07),
                    ValorNominal = 100000.04m,
                    Especie = Especie.CartaoCredito,
                    DataEmissao = new DateTime(2021, 01, 07),
                    ValorIof = 100000.04m,
                    IdTituloEmpresa = "RE-201609/1",
                    CodigoBarras = "04196861100000150502100100000010000000004089",
                    LinhaDigitavel = "04192100180000001000900000040899686110000015050",
                    Carteira = Carteira.CobrancaSimples,
                    Pagador = new Pagador
                    {
                        TipoPessoa = TipoPessoa.PessoaFisica,
                        CpfCnpj = "67524451091",
                        Nome = "PAGADOR FICTICIO",
                        Endereco = "RUA CALDAS JUNIOR 120",
                        Cep = "90010260",
                        Cidade = "PORTO ALEGRE",
                        Uf = "RS",
                        Aceite = Aceite.Sim
                    },
                    Sacador = new Sacador
                    {
                        TipoPessoa = TipoPessoa.PessoaFisica,
                        CpfCnpj = "67524451091",
                        Nome = "SACADOR FICTICIO",
                        Endereco = "RUA CALDAS JUNIOR 120",
                        Cep = "90010260"
                    },
                    Instrucoes = new Instrucoes
                    {
                        Juros = new Juros
                        {
                            Codigo = CodigoJuros.Isento,
                            Data = new DateTime(2021, 01, 07),
                            Valor = 100000.04m,
                            Taxa = 1.04m
                        },
                        Multa = new Multa
                        {
                            Codigo = CodigoMulta.ValorFixo,
                            Data = new DateTime(2021, 01, 07),
                            Valor = 100.04m,
                            Taxa = 0.4m
                        },
                        Desconto = new Desconto
                        {
                            Codigo = CodigoDesconto.ValorPorAntecipacaoDiaCorrido,
                            Data = new DateTime(2021, 01, 07),
                            Valor = 6.95m,
                            Taxa = 0.45m
                        },
                        Abatimento = new Abatimento
                        {
                            Valor = 6.80m
                        },
                        Protesto = new Protesto
                        {
                            Codigo = CodigoProtesto.NaoProtestar,
                            Prazo = 30
                        },
                        Baixa = new Baixa
                        {
                            Codigo = CodigoBaixa.BaixarDevolver,
                            Prazo = 30
                        }
                    },
                    PagParcial = new PagamentoParcial
                    {
                        Autoriza = AutorizacaoPagamentoParcial.Autoriza,
                        Codigo = CodigoPagamentoParcial.NaoAceitaDivergente,
                        Quantidade = 3,
                        Tipo = TipoValor.Percentual,
                        ValorMin = 18.30m,
                        ValorMax = 18.30m,
                        PercentualMin = 18.30m,
                        PercentualMax = 18.30m
                    },
                    Mensagens = new List<Mensagem>
                    {
                        new Mensagem
                        {
                            Linha = 3,
                            Texto = "Exemplo de mensagem"
                        }
                    },
                    Rateio = new Rateio
                    {
                        Codigo = CodigoRateio.ValorCobradoPagador,
                        TipoValor = TipoValor.Valor,
                        Beneficiarios = new List<BeneficiarioRateio>
                        {
                            new BeneficiarioRateio
                            {
                                Codigo = "0010000001088",
                                Valor = 16.25m,
                                Percentual = 16.253m,
                                Parcela = 2
                            }
                        }
                    },
                    Hibrido = new Hibrido
                    {
                        Autoriza = SimNao.Sim,
                        CopiaCola = "000201 010212 26580014BR.GOV.BCB.PIX0115example@email.com0208Compra5520400005303986540550.005802BR5913NOME DA LOJA6009CIDADE0006270400"
                    }
                }
            };
        }

        public static Boleto GenerateBoleto2()
        {
            var boleto = new Boleto
            {
                Ambiente = Ambiente.Teste,
                Titulo = new Titulo
                {
                    SeuNumero = "64452-1",
                    DataVencimento = new DateTime(2021, 5, 5),
                    ValorNominal = 150.50m,
                    Especie = Especie.Outros,
                    DataEmissao = new DateTime(2021, 3, 17),
                    IdTituloEmpresa = "0011000000000000000064452",
                    Pagador = new Pagador
                    {
                        TipoPessoa = TipoPessoa.PessoaFisica,
                        CpfCnpj = "00000000191",
                        Nome = "PAGADOR FICTICIO",
                        Endereco = "RUA CALDAS JUNIOR 120",
                        Cep = "90010260",
                        Cidade = "PORTO ALEGRE",
                        Uf = "RS",
                        Aceite = Aceite.Sim
                    },
                    Instrucoes = new Instrucoes
                    {
                        Juros = new Juros { Codigo = CodigoJuros.Isento },
                        Multa = new Multa
                        {
                            Codigo = CodigoMulta.Percentual,
                            Data = new DateTime(2021, 5, 6),
                            Taxa = 0.4m
                        },
                        Desconto = new Desconto
                        {
                            Codigo = CodigoDesconto.ValorFixoAteData,
                            Data = new DateTime(2021, 5, 3),
                            Valor = 6.95m
                        },
                        Abatimento = new Abatimento { Valor = 6.80m },
                        Protesto = new Protesto { Codigo = CodigoProtesto.NaoProtestar }
                    },
                    PagParcial = new PagamentoParcial
                    {
                        Autoriza = AutorizacaoPagamentoParcial.Autoriza,
                        Codigo = CodigoPagamentoParcial.NaoAceitaDivergente
                    }
                }
            };

            return boleto;
        }

        public static Boleto GenerateBoletoWithFillingErrors()
        {
            var boleto = new Boleto
            {
                Ambiente = Ambiente.Teste,
                Titulo = new Titulo
                {
                    NossoNumero = ""
                }
            };

            return boleto;
        }
    }
}
