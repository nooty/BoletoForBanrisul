using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;

namespace BoletoForBanrisul.Client.Tests.TestSupport
{
    /// <summary>
    /// Builds valid <see cref="Boleto"/> instances for Client-level tests. A separate, smaller copy of
    /// <c>BoletoForBanrisul.Core.Tests.BoletoFactory</c> - duplicated rather than shared because this project
    /// tests the HTTP surface (verbs, URLs, headers, error mapping), not the field-by-field validation rules
    /// the Core suite already covers in full, so only a minimal valid shape is needed here.
    /// </summary>
    internal static class BoletoFactory
    {
        /// <summary>The payer's CPF from the bank's own sandbox samples ("Homologado com nosso número"). Passes the check-digit algorithm.</summary>
        internal const string ValidCpf = "00000000191";

        /// <summary>The minimum valid bank slip: only the fields the bank's own schema requires.</summary>
        internal static Boleto CreateValid() => new Boleto
        {
            Ambiente = Ambiente.Teste,
            Titulo = new Titulo
            {
                SeuNumero = "64452-1",
                DataVencimento = new DateTime(2026, 8, 30),
                DataEmissao = new DateTime(2026, 7, 29),
                ValorNominal = 150.50m,
                Especie = Especie.Outros,
                Pagador = new Pagador
                {
                    TipoPessoa = TipoPessoa.PessoaFisica,
                    CpfCnpj = ValidCpf,
                    Nome = "PAGADOR FICTICIO",
                    Endereco = "RUA CALDAS JUNIOR 120",
                    Cep = "90010260",
                    Cidade = "PORTO ALEGRE",
                    Uf = "RS",
                    Aceite = Aceite.Sim
                }
            }
        };
    }
}
