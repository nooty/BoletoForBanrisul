using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models.Types;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Core.Models
{
    public class Boleto
    {
        [JsonConverter(typeof(AmbienteConverter))]
        public Ambiente Ambiente { get; set; } = Ambiente.Teste;

        public Titulo Titulo { get; set; } = new Titulo();
    }

    public class Titulo
    {
        [TruncateString(10)]
        public string? NossoNumero { get; set; }

        [TruncateString(13)]
        public string SeuNumero { get; set; } = "";

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime DataVencimento { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal ValorNominal { get; set; }

        [JsonConverter(typeof(EspecieConverter))]
        public Especie Especie { get; set; } = Especie.Outros;

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime DataEmissao { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorIof { get; set; }

        [TruncateString(25)]
        public string? IdTituloEmpresa { get; set; }

        public Pagador Pagador { get; set; } = new Pagador();

        public Sacador? Sacador { get; set; }

        public Instrucoes Instrucoes { get; set; } = new Instrucoes();

        public PagamentoParcial PagParcial { get; set; } = new PagamentoParcial();

        public List<Mensagem>? Mensagens { get; set; }

        public Rateio? Rateio { get; set; }

        public Hibrido? Hibrido { get; set; }

        /// <summary>Invoices (Notas Fiscais) linked to the bank slip. At most two per bank slip.</summary>
        public List<NotaFiscal>? NotasFiscais { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public Beneficiario? Beneficiario { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public string? CodigoBarras { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public string? LinhaDigitavel { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        [JsonConverter(typeof(SituacaoBanrisulConverter))]
        public SituacaoBanrisul? SituacaoBanrisul { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        [JsonConverter(typeof(SituacaoCipConverter))]
        public SituacaoCip? SituacaoCip { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public SituacaoPagamento? SituacaoPagamento { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public Carteira? Carteira { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public Operacoes? Operacoes { get; set; }
    }

    public class Beneficiario
    {
        [TruncateString(13)]
        public string Codigo { get; set; } = "";

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        [JsonConverter(typeof(TipoPessoaConverter))]
        public TipoPessoa TipoPessoa { get; set; } = TipoPessoa.PessoaJuridica;

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public string CpfCnpj { get; set; } = "";

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public string Nome { get; set; } = "";

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public string NomeFantasia { get; set; } = "";
    }

    public class Pagador
    {
        [JsonConverter(typeof(TipoPessoaConverter))]
        public TipoPessoa TipoPessoa { get; set; }

        [TruncateString(14)]
        public string CpfCnpj { get; set; } = "";

        [TruncateString(40)]
        public string Nome { get; set; } = "";

        [TruncateString(40)]
        public string Endereco { get; set; } = "";

        [TruncateString(8)]
        public string Cep { get; set; } = "";

        [TruncateString(15)]
        public string Cidade { get; set; } = "";

        [TruncateString(2)]
        public string Uf { get; set; } = "";

        [JsonConverter(typeof(AceiteConverter))]
        public Aceite Aceite { get; set; } = Aceite.Nao;
    }

    public class Sacador
    {
        [JsonConverter(typeof(TipoPessoaConverter))]
        public TipoPessoa TipoPessoa { get; set; }

        [TruncateString(14)]
        public string CpfCnpj { get; set; } = "";

        [TruncateString(45)]
        public string Nome { get; set; } = "";

        [TruncateString(45)]
        public string Endereco { get; set; } = "";

        [TruncateString(8)]
        public string Cep { get; set; } = "";
    }

    public class Instrucoes
    {
        public Juros Juros { get; set; } = new Juros();

        public Multa? Multa { get; set; }

        public Desconto? Desconto { get; set; }

        public Abatimento? Abatimento { get; set; }

        public Protesto? Protesto { get; set; }

        public Baixa? Baixa { get; set; }
    }

    public class Juros
    {
        public CodigoJuros Codigo { get; set; } = CodigoJuros.Isento;

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? Data { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Valor { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Taxa { get; set; }
    }

    public class Multa
    {
        public CodigoMulta Codigo { get; set; }

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? Data { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Valor { get; set; }

        [JsonConverter(typeof(DecimalF1Converter))]
        public decimal? Taxa { get; set; }
    }

    public class Desconto
    {
        public CodigoDesconto Codigo { get; set; }

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? Data { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Valor { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Taxa { get; set; }
    }

    public class Abatimento
    {
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal Valor { get; set; }
    }

    public class Protesto
    {
        public CodigoProtesto Codigo { get; set; }

        /// <summary>
        /// Número de dias corridos após a data de vencimento para inicialização do processo de cobrança via protesto.
        /// </summary>
        public int? Prazo { get; set; }
    }

    public class Baixa
    {
        public CodigoBaixa Codigo { get; set; }

        /// <summary>
        /// Número de dias corridos após a data de vencimento, que deverá ser baixado/devolvido para o Beneficiário.
        /// </summary>
        public int Prazo { get; set; }
    }

    public class PagamentoParcial
    {
        public AutorizacaoPagamentoParcial Autoriza { get; set; } = AutorizacaoPagamentoParcial.NaoAutoriza;

        public CodigoPagamentoParcial Codigo { get; set; } = CodigoPagamentoParcial.NaoAceitaDivergente;

        /// <summary>
        /// Quantidade de pagamentos parciais possíveis (opcional).
        /// </summary>
        public int? Quantidade { get; set; }

        public TipoValor? Tipo { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorMin { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorMax { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? PercentualMin { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? PercentualMax { get; set; }
    }

    public class Mensagem
    {
        public int Linha { get; set; }

        [TruncateString(75)]
        public string Texto { get; set; } = "";
    }

    /// <summary>An invoice (Nota Fiscal) linked to the bank slip. A bank slip carries at most two.</summary>
    public class NotaFiscal
    {
        /// <summary>DANFE access key: 43 numeric characters plus a check digit, 44 in total.</summary>
        public string Danfe { get; set; } = "";
    }

    public class Rateio
    {
        public CodigoRateio Codigo { get; set; }

        public TipoValor TipoValor { get; set; }

        public List<BeneficiarioRateio> Beneficiarios { get; set; } = new List<BeneficiarioRateio>();
    }

    public class BeneficiarioRateio
    {
        public string Codigo { get; set; } = "";

        /// <summary>
        /// CPF/CNPJ of the sharing beneficiary. Sent on the request and also echoed back by the bank.
        /// </summary>
        public string? CpfCnpj { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Valor { get; set; }

        [JsonConverter(typeof(DecimalF3Converter))]
        public decimal? Percentual { get; set; }

        public int? Parcela { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        [JsonConverter(typeof(TipoPessoaConverter))]
        public TipoPessoa? TipoPessoa { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public string? Nome { get; set; }

        /// <summary>
        /// Retornado pelo banco.
        /// </summary>
        public string? NomeFantasia { get; set; }
    }

    public class Hibrido
    {
        /// <summary>
        /// Indica se a operação híbrida foi autorizada.
        /// Pode assumir os valores:
        /// S = Sim
        /// N = Não
        /// </summary>
        [JsonConverter(typeof(SimNaoConverter))]
        public SimNao Autoriza { get; set; }

        /// <summary>
        /// Situação do registro. Pode assumir os valores:
        /// A = Criado
        /// N = Não criado
        /// P = Pendente
        /// Retornado pelo banco.
        /// </summary>
        [JsonConverter(typeof(SituacaoHibridoConverter))]
        public SituacaoHibrido? Situacao { get; set; }

        /// <summary>
        /// Identificador da transação (opcional).
        /// Campo numérico de até 33 caracteres.
        /// Retornado pelo banco.
        /// </summary>
        public string? Txid { get; set; }

        /// <summary>
        /// Endereço do QR Code registrado no banco (opcional).
        /// Campo alfanumérico de até 77 caracteres.
        /// Retornado pelo banco.
        /// </summary>
        public string? Location { get; set; }

        /// <summary>
        /// Texto "Copia &amp; Cola" responsável pela geração do QR Code
        /// para pagamento via Pix (opcional).
        /// Campo alfanumérico de até 512 caracteres.
        /// Retornado pelo banco.
        /// </summary>
        public string? CopiaCola { get; set; }
    }

    public class Operacoes
    {
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? DataBaixa { get; set; }
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? DataPagamento { get; set; }
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? DataCredito { get; set; }
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? DataReembolso { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorJurosRecebido { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorJurosPago { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorMultaRecebido { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorDescontoUtilizado { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorAbatimentoUtilizado { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorIof { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? CustasCartorio { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? RessarcimentoCartorio { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? TarifaRegistro { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? TarifaBaixaLiquidacao { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? TarifaAponteCartorio { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? TarifaInterbancaria { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? TarifaManutencaoMensal { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? TarifaSustacao { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? TarifasDiversas { get; set; }

        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorPagamento { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorReembolso { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorCobrado { get; set; }
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorCreditadoDebitado { get; set; }
    }

    public static class BoletoExtensions
    {
        public static string ToJson(this Boleto boleto, bool indented = true)
        {
            return JsonSerializer.Serialize(boleto, new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = new SnakeCaseLowerNamingPolicy()
            });
        }
    }
}
