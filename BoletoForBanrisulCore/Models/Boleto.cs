using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models.Types;
using BoletoForBanrisul.Core.Validation;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Core.Models
{
    public class Boleto
    {
        [EnumDataType(typeof(Ambiente), ErrorMessage = "Ambiente must be one of the defined Ambiente values.")]
        [JsonConverter(typeof(AmbienteConverter))]
        public Ambiente Ambiente { get; set; } = Ambiente.Teste;

        [Required]
        public Titulo Titulo { get; set; } = new Titulo();
    }

    /// <summary>
    /// A bank slip's core data: identification, amounts, payer, instructions and (once returned by the
    /// bank) barcode/status information.
    /// </summary>
    /// <remarks>
    /// <b>This class is shared between the request and the response.</b> <c>CreateBoletoResponse.Titulo</c>
    /// and <c>GetBoletoResponse.Titulo</c> both deserialize into this same <see cref="Titulo"/> type, but
    /// <see cref="Validation.RecursiveValidator"/> only ever runs on the root of an outgoing
    /// <see cref="Boleto"/> request (see <c>BoletoClient.CreateBoletoAsync</c>). What actually protects a
    /// response round-trip is that call graph, not the attributes below — but the attributes must not fight
    /// it: a bank-returned ("BR") property — <see cref="Beneficiario"/>, <see cref="CodigoBarras"/>,
    /// <see cref="LinhaDigitavel"/>, the <c>Situacao*</c> properties, <see cref="Carteira"/>,
    /// <see cref="Operacoes"/> — must never get <see cref="RequiredAttribute"/> or a cross-field
    /// required-ness rule in <see cref="Validate"/>, because deserializing a query response that legitimately
    /// omits one of them would then have no path that ever validates it. Format-only attributes (length,
    /// character class, numeric range) are safe on both paths and are used freely below.
    /// </remarks>
    public class Titulo : IValidatableObject
    {
        /// <summary>
        /// Not required and not exactly 10 digits, even though the bank's own response schema requires
        /// exactly 10: the request is looser than the response (the bank's own "Homologado com nosso número"
        /// example sends <c>"264"</c>, 3 digits) and the request wins where the two disagree.
        /// </summary>
        [StringLength(10)]
        [RegularExpression(BanrisulPatterns.DigitsUpTo10)]
        [TruncateString(10)]
        public string? NossoNumero { get; set; }

        [Required]
        [StringLength(13, MinimumLength = 1)]
        [RegularExpression(BanrisulPatterns.Reference)]
        [TruncateString(13)]
        public string SeuNumero { get; set; } = "";

        /// <summary>Due date of the bank slip. Required, but a non-nullable <see cref="DateTime"/> cannot carry <see cref="RequiredAttribute"/>; see <see cref="Validate"/> for the <c>!= default</c> check.</summary>
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime DataVencimento { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal ValorNominal { get; set; }

        [EnumDataType(typeof(Especie), ErrorMessage = "Especie must be one of the defined Especie values.")]
        [JsonConverter(typeof(EspecieConverter))]
        public Especie Especie { get; set; } = Especie.Outros;

        /// <summary>Issue date of the bank slip. Required, but a non-nullable <see cref="DateTime"/> cannot carry <see cref="RequiredAttribute"/>; see <see cref="Validate"/> for the <c>!= default</c> check.</summary>
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime DataEmissao { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorIof { get; set; }

        [StringLength(25)]
        [RegularExpression(BanrisulPatterns.Reference)]
        [TruncateString(25)]
        public string? IdTituloEmpresa { get; set; }

        [Required]
        public Pagador Pagador { get; set; } = new Pagador();

        public Sacador? Sacador { get; set; }

        [Required]
        public Instrucoes Instrucoes { get; set; } = new Instrucoes();

        [Required]
        public PagamentoParcial PagParcial { get; set; } = new PagamentoParcial();

        [MinLength(1)]
        [MaxLength(9)]
        public List<Mensagem>? Mensagens { get; set; }

        public Rateio? Rateio { get; set; }

        public Hibrido? Hibrido { get; set; }

        /// <summary>Invoices (Notas Fiscais) linked to the bank slip. At most two per bank slip.</summary>
        [MinLength(1)]
        [MaxLength(2)]
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

        /// <summary>
        /// Cross-field rules that need more than one of <see cref="Titulo"/>'s own properties, or that need
        /// this object's own dates/amount together with a nested instruction. Rules about the internal
        /// consistency of a single nested object live on that object's own
        /// <see cref="IValidatableObject.Validate"/> instead (see <see cref="Pagador"/>, <see cref="Sacador"/>,
        /// <see cref="Juros"/>, <see cref="Multa"/>, <see cref="Desconto"/>, <see cref="Rateio"/> and
        /// <see cref="PagamentoParcial"/>) — <see cref="Validation.RecursiveValidator"/> walks into all of
        /// them and aggregates every error from the whole graph in one pass, so splitting the rules this way
        /// does not lose anything. Every error reported here about a nested member uses a dotted path (e.g.
        /// <c>"Instrucoes.Desconto.Data"</c>); <c>MiniValidation</c> prefixes it with the path to reach this
        /// <see cref="Titulo"/> itself, so the final reported path is <c>"Titulo.Instrucoes.Desconto.Data"</c>.
        /// </summary>
        /// <remarks>
        /// Every nested object is reached through <c>?.</c>, including the ones declared non-nullable with an
        /// initializer (<see cref="Instrucoes"/>, <see cref="Instrucoes.Juros"/>, <see cref="PagParcial"/>,
        /// <see cref="Rateio.Beneficiarios"/>). That is not defensive habit: <c>MiniValidation</c> calls this
        /// method <b>unconditionally</b>, before and regardless of the <see cref="RequiredAttribute"/> results
        /// on those same properties, so a <c>null</c> arriving from the wire (this type also deserializes the
        /// bank's responses — see the <c>&lt;remarks&gt;</c> on <see cref="Titulo"/>) would throw here and
        /// abort the whole graph's validation, swallowing every error including the <c>[Required]</c> that was
        /// about to name the missing object. Skipping the rule instead lets that attribute report it.
        /// </remarks>
        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            ValidateRequiredDates(results);           // Rules 1-2, SPEC
            ValidateDueDateNotBeforeIssueDate(results); // Rule 3, SPEC
            ValidateDiscountDateWindow(results);       // Rule 4, SPEC
            ValidateInterestDateAfterDueDate(results);  // Rule 5, SPEC
            ValidateFineDateAfterDueDate(results);      // Rule 6, SPEC
            ValidateAbatementAgainstNominalValue(results); // Rule 7, PARTIAL
            ValidateProtestDeadline(results);           // Rule 8, SPEC
            ValidatePartialPaymentAuthorization(results); // Rules 9-10, SPEC
            ValidateNominalValuePositive(results);       // Rule 11, INFERRED
            ValidateDistinctMessageLines(results);       // Rule 12, INFERRED
            ValidateSharingValueAgainstNominalValue(results); // Rule 13, INFERRED

            return results;
        }

        /// <summary>SPEC: <see cref="DataVencimento"/> and <see cref="DataEmissao"/> are required; since neither is nullable, <see cref="RequiredAttribute"/> cannot express that, so it is checked here as "not the default value" instead.</summary>
        private void ValidateRequiredDates(List<ValidationResult> results)
        {
            if (DataVencimento == default)
                results.Add(new ValidationResult("DataVencimento is required.", new[] { nameof(DataVencimento) }));

            if (DataEmissao == default)
                results.Add(new ValidationResult("DataEmissao is required.", new[] { nameof(DataEmissao) }));
        }

        /// <summary>SPEC ("RN validade menor do que emissão"): <see cref="DataVencimento"/> must not be earlier than <see cref="DataEmissao"/>.</summary>
        private void ValidateDueDateNotBeforeIssueDate(List<ValidationResult> results)
        {
            if (DataVencimento.Date < DataEmissao.Date)
            {
                results.Add(new ValidationResult(
                    "DataVencimento must not be earlier than DataEmissao.",
                    new[] { nameof(DataVencimento) }));
            }
        }

        /// <summary>SPEC: <c>desconto.data</c> "Deve ser menor ou igual a Data de Vencimento. Deve ser maior ou igual a Data de Emissão."</summary>
        private void ValidateDiscountDateWindow(List<ValidationResult> results)
        {
            var data = Instrucoes?.Desconto?.Data;
            if (data is not DateTime discountDate)
                return;

            if (discountDate.Date < DataEmissao.Date || discountDate.Date > DataVencimento.Date)
            {
                results.Add(new ValidationResult(
                    "Instrucoes.Desconto.Data must be between DataEmissao and DataVencimento (inclusive).",
                    new[] { "Instrucoes.Desconto.Data" }));
            }
        }

        /// <summary>SPEC: <c>juros.data</c> "Deve ser maior que a data de vencimento" (strictly later).</summary>
        private void ValidateInterestDateAfterDueDate(List<ValidationResult> results)
        {
            var data = Instrucoes?.Juros?.Data;
            if (data is not DateTime interestDate)
                return;

            if (interestDate.Date <= DataVencimento.Date)
            {
                results.Add(new ValidationResult(
                    "Instrucoes.Juros.Data must be strictly later than DataVencimento.",
                    new[] { "Instrucoes.Juros.Data" }));
            }
        }

        /// <summary>SPEC: <c>multa.data</c> "Deve ser maior que a Data de Vencimento" (strictly later).</summary>
        private void ValidateFineDateAfterDueDate(List<ValidationResult> results)
        {
            var data = Instrucoes?.Multa?.Data;
            if (data is not DateTime fineDate)
                return;

            if (fineDate.Date <= DataVencimento.Date)
            {
                results.Add(new ValidationResult(
                    "Instrucoes.Multa.Data must be strictly later than DataVencimento.",
                    new[] { "Instrucoes.Multa.Data" }));
            }
        }

        /// <summary>
        /// PARTIAL: the bank's own rule ("RN soma excede valor boleto") checks the abatement against
        /// everything accumulated on the live bank slip server-side, which this library cannot see. This is
        /// the subset that is checkable locally: a single abatement cannot by itself exceed the nominal value.
        /// </summary>
        private void ValidateAbatementAgainstNominalValue(List<ValidationResult> results)
        {
            var valor = Instrucoes?.Abatimento?.Valor;
            if (valor is not decimal abatementValue)
                return;

            if (abatementValue > ValorNominal)
            {
                results.Add(new ValidationResult(
                    "Instrucoes.Abatimento.Valor must not exceed ValorNominal.",
                    new[] { "Instrucoes.Abatimento.Valor" }));
            }
        }

        /// <summary>
        /// SPEC (<c>protesto.prazo</c> description): required when <see cref="Codigo"/> is
        /// <see cref="CodigoProtesto.ProtestarDiasCorridos"/> or <see cref="CodigoProtesto.NegativarDiasCorridos"/>.
        /// <c>Prazo == 0</c> is immediate protest and is only accepted for a bank slip already past due;
        /// otherwise <c>Prazo</c> must be at least 3.
        /// </summary>
        private void ValidateProtestDeadline(List<ValidationResult> results)
        {
            var protesto = Instrucoes?.Protesto;
            if (protesto is null)
                return;

            bool needsDeadline = protesto.Codigo is Types.CodigoProtesto.ProtestarDiasCorridos or Types.CodigoProtesto.NegativarDiasCorridos;
            if (!needsDeadline)
                return;

            if (protesto.Prazo is not int prazo)
            {
                results.Add(new ValidationResult(
                    $"Instrucoes.Protesto.Prazo is required when Instrucoes.Protesto.Codigo is {protesto.Codigo}.",
                    new[] { "Instrucoes.Protesto.Prazo" }));
                return;
            }

            bool isOverdue = DataVencimento.Date < DateTime.Today;

            if (prazo == 0 && !isOverdue)
            {
                results.Add(new ValidationResult(
                    "Instrucoes.Protesto.Prazo of 0 (immediate protest) is only accepted for a bank slip already past due.",
                    new[] { "Instrucoes.Protesto.Prazo" }));
            }
            else if (prazo != 0 && prazo < 3)
            {
                results.Add(new ValidationResult(
                    "Instrucoes.Protesto.Prazo must be at least 3 for a bank slip not yet due, or exactly 0 for one already past due.",
                    new[] { "Instrucoes.Protesto.Prazo" }));
            }
        }

        /// <summary>
        /// SPEC (<c>pag_parcial.autoriza</c> description): must always be <see cref="AutorizacaoPagamentoParcial.NaoAutoriza"/>,
        /// except when <see cref="Especie"/> is <see cref="Types.Especie.CartaoCredito"/> (Rule 9), and must
        /// always be <see cref="AutorizacaoPagamentoParcial.NaoAutoriza"/> when <see cref="Hibrido"/> is set (Rule 10).
        /// </summary>
        private void ValidatePartialPaymentAuthorization(List<ValidationResult> results)
        {
            if (PagParcial?.Autoriza != Types.AutorizacaoPagamentoParcial.Autoriza)
                return;

            if (Especie != Types.Especie.CartaoCredito)
            {
                results.Add(new ValidationResult(
                    "PagParcial.Autoriza must be NaoAutoriza unless Especie is CartaoCredito.",
                    new[] { "PagParcial.Autoriza" }));
            }

            if (Hibrido is not null)
            {
                results.Add(new ValidationResult(
                    "PagParcial.Autoriza must be NaoAutoriza when Hibrido is set.",
                    new[] { "PagParcial.Autoriza" }));
            }
        }

        /// <summary>INFERRED: the exception exists because the bank's own "Cartão de Crédito" sample registers <c>"0.00"</c>.</summary>
        private void ValidateNominalValuePositive(List<ValidationResult> results)
        {
            if (Especie == Types.Especie.CartaoCredito)
                return;

            if (ValorNominal <= 0)
            {
                results.Add(new ValidationResult(
                    "ValorNominal must be greater than zero unless Especie is CartaoCredito.",
                    new[] { nameof(ValorNominal) }));
            }
        }

        /// <summary>INFERRED.</summary>
        private void ValidateDistinctMessageLines(List<ValidationResult> results)
        {
            if (Mensagens is null || Mensagens.Count == 0)
                return;

            var duplicateLines = Mensagens
                .GroupBy(m => m.Linha)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var linha in duplicateLines)
            {
                results.Add(new ValidationResult(
                    $"Mensagens line {linha} is used more than once; Linha must be distinct across Mensagens.",
                    new[] { "Mensagens" }));
            }
        }

        /// <summary>INFERRED.</summary>
        private void ValidateSharingValueAgainstNominalValue(List<ValidationResult> results)
        {
            if (Rateio?.Beneficiarios is null || Rateio.TipoValor != Types.TipoValor.Valor)
                return;

            decimal total = Rateio.Beneficiarios.Sum(b => b.Valor ?? 0m);
            if (total > ValorNominal)
            {
                results.Add(new ValidationResult(
                    $"Rateio.Beneficiarios values must sum to at most ValorNominal ({ValorNominal}), but summed to {total}.",
                    new[] { "Rateio.Beneficiarios" }));
            }
        }
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

    public class Pagador : IValidatableObject
    {
        [EnumDataType(typeof(TipoPessoa), ErrorMessage = "TipoPessoa must be one of the defined TipoPessoa values.")]
        [JsonConverter(typeof(TipoPessoaConverter))]
        public TipoPessoa TipoPessoa { get; set; }

        [Required]
        [RegularExpression(BanrisulPatterns.CpfOrCnpj)]
        [TruncateString(14)]
        public string CpfCnpj { get; set; } = "";

        [Required]
        [StringLength(40, MinimumLength = 1)]
        [RegularExpression(BanrisulPatterns.Text)]
        [TruncateString(40)]
        public string Nome { get; set; } = "";

        [Required]
        [StringLength(40, MinimumLength = 1)]
        [RegularExpression(BanrisulPatterns.Text)]
        [TruncateString(40)]
        public string Endereco { get; set; } = "";

        [Required]
        [RegularExpression(BanrisulPatterns.Digits8)]
        [TruncateString(8)]
        public string Cep { get; set; } = "";

        [Required]
        [StringLength(15, MinimumLength = 1)]
        [RegularExpression(BanrisulPatterns.Text)]
        [TruncateString(15)]
        public string Cidade { get; set; } = "";

        [Required]
        [StringLength(2, MinimumLength = 2)]
        [RegularExpression(BanrisulPatterns.Uf)]
        [TruncateString(2)]
        public string Uf { get; set; } = "";

        [EnumDataType(typeof(Aceite), ErrorMessage = "Aceite must be one of the defined Aceite values.")]
        [JsonConverter(typeof(AceiteConverter))]
        public Aceite Aceite { get; set; } = Aceite.Nao;

        /// <summary>
        /// CPF/CNPJ check-digit validation: <see cref="CpfCnpj"/> must have the length required by
        /// <see cref="TipoPessoa"/> (11 digits for CPF, 14 for CNPJ) and pass its check-digit algorithm. The
        /// regular expression on <see cref="CpfCnpj"/> only enforces "11 or 14 digits", not which one is
        /// right for the declared <see cref="TipoPessoa"/>, hence the cross-field check here.
        /// </summary>
        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
            DocumentNumberValidator.Validate(TipoPessoa, CpfCnpj, nameof(CpfCnpj));
    }

    public class Sacador : IValidatableObject
    {
        [EnumDataType(typeof(TipoPessoa), ErrorMessage = "TipoPessoa must be one of the defined TipoPessoa values.")]
        [JsonConverter(typeof(TipoPessoaConverter))]
        public TipoPessoa TipoPessoa { get; set; }

        [Required]
        [RegularExpression(BanrisulPatterns.CpfOrCnpj)]
        [TruncateString(14)]
        public string CpfCnpj { get; set; } = "";

        // 40, not 45: the schema's maxLength for sacador.nome is 40 (identical fragment to pagador.nome,
        // already 40 above). [TruncateString] is changed to match so the dead truncation path never
        // contradicts the live [StringLength] rule; see also Test/Unit/TruncateStringTest.cs.
        [Required]
        [StringLength(40, MinimumLength = 1)]
        [RegularExpression(BanrisulPatterns.Text)]
        [TruncateString(40)]
        public string Nome { get; set; } = "";

        [Required]
        [StringLength(40, MinimumLength = 1)]
        [RegularExpression(BanrisulPatterns.Text)]
        [TruncateString(40)]
        public string Endereco { get; set; } = "";

        [Required]
        [RegularExpression(BanrisulPatterns.Digits8)]
        [TruncateString(8)]
        public string Cep { get; set; } = "";

        /// <summary>See <see cref="Pagador.Validate"/>: same CPF/CNPJ check-digit cross-check.</summary>
        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
            DocumentNumberValidator.Validate(TipoPessoa, CpfCnpj, nameof(CpfCnpj));
    }

    public class Instrucoes
    {
        [Required]
        public Juros Juros { get; set; } = new Juros();

        public Multa? Multa { get; set; }

        public Desconto? Desconto { get; set; }

        public Abatimento? Abatimento { get; set; }

        public Protesto? Protesto { get; set; }

        public Baixa? Baixa { get; set; }
    }

    public class Juros : IValidatableObject
    {
        /// <summary>
        /// Code for the interest instruction. <see cref="Types.CodigoJuros.Isento"/> is the default and is
        /// deliberately not cross-checked against <see cref="Data"/>/<see cref="Valor"/>/<see cref="Taxa"/>
        /// being unset: the schema does not require that, and this repository's own
        /// <c>BoletoGenerator.GenerateBoleto1()</c> sets all three alongside <see cref="Types.CodigoJuros.Isento"/>.
        /// Only <see cref="Types.CodigoJuros.ValorPorDia"/> and <see cref="Types.CodigoJuros.TaxaMensal"/> are
        /// cross-checked, in <see cref="Validate"/>.
        /// </summary>
        [EnumDataType(typeof(CodigoJuros), ErrorMessage = "Codigo must be one of the defined CodigoJuros values.")]
        public CodigoJuros Codigo { get; set; } = CodigoJuros.Isento;

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? Data { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Valor { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.Rate3Max)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Taxa { get; set; }

        /// <summary>
        /// SPEC: <see cref="Types.CodigoJuros.ValorPorDia"/> requires <see cref="Valor"/> (and not <see cref="Taxa"/>);
        /// <see cref="Types.CodigoJuros.TaxaMensal"/> requires <see cref="Taxa"/> (and not <see cref="Valor"/>).
        /// The "required" half is SPEC (the schema's own field descriptions); the "must be unset" half is
        /// INFERRED. Deliberately does <b>not</b> check <see cref="Types.CodigoJuros.Isento"/> — see the XML
        /// doc on <see cref="Codigo"/>.
        /// </summary>
        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (Codigo == CodigoJuros.ValorPorDia)
            {
                if (!Valor.HasValue)
                    results.Add(new ValidationResult("Valor is required when Codigo is ValorPorDia.", new[] { nameof(Valor) }));
                if (Taxa.HasValue)
                    results.Add(new ValidationResult("Taxa must be unset when Codigo is ValorPorDia.", new[] { nameof(Taxa) }));
            }
            else if (Codigo == CodigoJuros.TaxaMensal)
            {
                if (!Taxa.HasValue)
                    results.Add(new ValidationResult("Taxa is required when Codigo is TaxaMensal.", new[] { nameof(Taxa) }));
                if (Valor.HasValue)
                    results.Add(new ValidationResult("Valor must be unset when Codigo is TaxaMensal.", new[] { nameof(Valor) }));
            }

            return results;
        }
    }

    public class Multa : IValidatableObject
    {
        [EnumDataType(typeof(CodigoMulta), ErrorMessage = "Codigo must be one of the defined CodigoMulta values.")]
        public CodigoMulta Codigo { get; set; }

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? Data { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Valor { get; set; }

        // F1 case: Taxa is serialized by DecimalF1Converter ("F1"), so the scale is 1, not 2.
        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.Rate4Max)]
        [DecimalScale(1)]
        [JsonConverter(typeof(DecimalF1Converter))]
        public decimal? Taxa { get; set; }

        /// <summary>SPEC: <see cref="Types.CodigoMulta.ValorFixo"/> requires <see cref="Valor"/> (not <see cref="Taxa"/>); <see cref="Types.CodigoMulta.Percentual"/> requires <see cref="Taxa"/> (not <see cref="Valor"/>).</summary>
        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (Codigo == CodigoMulta.ValorFixo)
            {
                if (!Valor.HasValue)
                    results.Add(new ValidationResult("Valor is required when Codigo is ValorFixo.", new[] { nameof(Valor) }));
                if (Taxa.HasValue)
                    results.Add(new ValidationResult("Taxa must be unset when Codigo is ValorFixo.", new[] { nameof(Taxa) }));
            }
            else if (Codigo == CodigoMulta.Percentual)
            {
                if (!Taxa.HasValue)
                    results.Add(new ValidationResult("Taxa is required when Codigo is Percentual.", new[] { nameof(Taxa) }));
                if (Valor.HasValue)
                    results.Add(new ValidationResult("Valor must be unset when Codigo is Percentual.", new[] { nameof(Valor) }));
            }

            return results;
        }
    }

    public class Desconto : IValidatableObject
    {
        // The enum already omits 4 - it is exactly the schema's ^([1-3]|5)$.
        [EnumDataType(typeof(CodigoDesconto), ErrorMessage = "Codigo must be one of the defined CodigoDesconto values.")]
        public CodigoDesconto Codigo { get; set; }

        [JsonConverter(typeof(DateOnlyConverter))]
        public DateTime? Data { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Valor { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.Rate3Max)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Taxa { get; set; }

        /// <summary>
        /// SPEC: <see cref="Types.CodigoDesconto.ValorFixoAteData"/> and
        /// <see cref="Types.CodigoDesconto.ValorPorAntecipacaoDiaCorrido"/> require <see cref="Valor"/> (not
        /// <see cref="Taxa"/>); <see cref="Types.CodigoDesconto.PercentualAteData"/> and
        /// <see cref="Types.CodigoDesconto.PercentualSobreValorNominalDiaCorrido"/> require <see cref="Taxa"/>
        /// (not <see cref="Valor"/>).
        /// </summary>
        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            bool isFixedValueCode = Codigo is CodigoDesconto.ValorFixoAteData or CodigoDesconto.ValorPorAntecipacaoDiaCorrido;
            bool isPercentageCode = Codigo is CodigoDesconto.PercentualAteData or CodigoDesconto.PercentualSobreValorNominalDiaCorrido;

            if (isFixedValueCode)
            {
                if (!Valor.HasValue)
                    results.Add(new ValidationResult($"Valor is required when Codigo is {Codigo}.", new[] { nameof(Valor) }));
                if (Taxa.HasValue)
                    results.Add(new ValidationResult($"Taxa must be unset when Codigo is {Codigo}.", new[] { nameof(Taxa) }));
            }
            else if (isPercentageCode)
            {
                if (!Taxa.HasValue)
                    results.Add(new ValidationResult($"Taxa is required when Codigo is {Codigo}.", new[] { nameof(Taxa) }));
                if (Valor.HasValue)
                    results.Add(new ValidationResult($"Valor must be unset when Codigo is {Codigo}.", new[] { nameof(Valor) }));
            }

            return results;
        }
    }

    public class Abatimento
    {
        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal Valor { get; set; }
    }

    public class Protesto
    {
        [EnumDataType(typeof(CodigoProtesto), ErrorMessage = "Codigo must be one of the defined CodigoProtesto values.")]
        public CodigoProtesto Codigo { get; set; }

        /// <summary>
        /// Número de dias corridos após a data de vencimento para inicialização do processo de cobrança via protesto.
        /// </summary>
        [Range(0, 99)]
        public int? Prazo { get; set; }
    }

    public class Baixa
    {
        [EnumDataType(typeof(CodigoBaixa), ErrorMessage = "Codigo must be one of the defined CodigoBaixa values.")]
        public CodigoBaixa Codigo { get; set; }

        /// <summary>
        /// Número de dias corridos após a data de vencimento, que deverá ser baixado/devolvido para o Beneficiário.
        /// </summary>
        [Range(0, 99)]
        public int Prazo { get; set; }
    }

    public class PagamentoParcial : IValidatableObject
    {
        [EnumDataType(typeof(AutorizacaoPagamentoParcial), ErrorMessage = "Autoriza must be one of the defined AutorizacaoPagamentoParcial values.")]
        public AutorizacaoPagamentoParcial Autoriza { get; set; } = AutorizacaoPagamentoParcial.NaoAutoriza;

        [EnumDataType(typeof(CodigoPagamentoParcial), ErrorMessage = "Codigo must be one of the defined CodigoPagamentoParcial values.")]
        public CodigoPagamentoParcial Codigo { get; set; } = CodigoPagamentoParcial.NaoAceitaDivergente;

        /// <summary>
        /// Quantidade de pagamentos parciais possíveis (opcional).
        /// </summary>
        [Range(0, 99)]
        public int? Quantidade { get; set; }

        [EnumDataType(typeof(TipoValor), ErrorMessage = "Tipo must be one of the defined TipoValor values.")]
        public TipoValor? Tipo { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorMin { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? ValorMax { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.Rate3Max)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? PercentualMin { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.Rate3Max)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? PercentualMax { get; set; }

        /// <summary>
        /// SPEC: <see cref="Autoriza"/> == <see cref="AutorizacaoPagamentoParcial.Autoriza"/> requires
        /// <see cref="Quantidade"/> to be set to at least 1. <see cref="Codigo"/> ==
        /// <see cref="CodigoPagamentoParcial.ValorEntreMinimoEMaximo"/> requires <see cref="Tipo"/> to be
        /// set. The rest is INFERRED and deliberately narrow — see the remarks below on what is
        /// <b>not</b> checked.
        /// </summary>
        /// <remarks>
        /// Does <b>not</b> implement: "<see cref="Tipo"/> only when <see cref="Codigo"/> is
        /// <see cref="CodigoPagamentoParcial.ValorEntreMinimoEMaximo"/>", "both bounds required", or "all
        /// bounds unset when <see cref="Codigo"/> is <see cref="CodigoPagamentoParcial.NaoAceitaDivergente"/>".
        /// The bank's own "Cartão de Crédito" sample sends <c>codigo: 1 (QualquerValor), tipo: 2 (Valor),
        /// valor_min: "1.50"</c> with no <c>valor_max</c> — every one of those three rules would reject that
        /// real, successful payload. Left out on purpose; do not reintroduce them.
        /// </remarks>
        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (Autoriza == AutorizacaoPagamentoParcial.Autoriza && (!Quantidade.HasValue || Quantidade.Value < 1))
            {
                results.Add(new ValidationResult(
                    "Quantidade is required and must be at least 1 when Autoriza is Autoriza.",
                    new[] { nameof(Quantidade) }));
            }

            if (Codigo == CodigoPagamentoParcial.ValorEntreMinimoEMaximo && !Tipo.HasValue)
            {
                results.Add(new ValidationResult(
                    "Tipo is required when Codigo is ValorEntreMinimoEMaximo.",
                    new[] { nameof(Tipo) }));
            }

            // INFERRED, mutual exclusion only - see remarks for what is deliberately not required.
            if (Tipo == TipoValor.Valor)
            {
                if (PercentualMin.HasValue)
                    results.Add(new ValidationResult("PercentualMin must be unset when Tipo is Valor.", new[] { nameof(PercentualMin) }));
                if (PercentualMax.HasValue)
                    results.Add(new ValidationResult("PercentualMax must be unset when Tipo is Valor.", new[] { nameof(PercentualMax) }));
            }
            else if (Tipo == TipoValor.Percentual)
            {
                if (ValorMin.HasValue)
                    results.Add(new ValidationResult("ValorMin must be unset when Tipo is Percentual.", new[] { nameof(ValorMin) }));
                if (ValorMax.HasValue)
                    results.Add(new ValidationResult("ValorMax must be unset when Tipo is Percentual.", new[] { nameof(ValorMax) }));
            }

            if (ValorMin.HasValue && ValorMax.HasValue && ValorMin.Value > ValorMax.Value)
            {
                results.Add(new ValidationResult(
                    "ValorMin must not be greater than ValorMax.",
                    new[] { nameof(ValorMin), nameof(ValorMax) }));
            }

            if (PercentualMin.HasValue && PercentualMax.HasValue && PercentualMin.Value > PercentualMax.Value)
            {
                results.Add(new ValidationResult(
                    "PercentualMin must not be greater than PercentualMax.",
                    new[] { nameof(PercentualMin), nameof(PercentualMax) }));
            }

            return results;
        }
    }

    public class Mensagem
    {
        [Range(1, 9)]
        public int Linha { get; set; }

        [Required]
        [StringLength(75, MinimumLength = 1)]
        [RegularExpression(BanrisulPatterns.Text)]
        [TruncateString(75)]
        public string Texto { get; set; } = "";
    }

    /// <summary>An invoice (Nota Fiscal) linked to the bank slip. A bank slip carries at most two.</summary>
    public class NotaFiscal
    {
        /// <summary>DANFE access key: 43 numeric characters plus a check digit, 44 in total.</summary>
        [Required]
        [RegularExpression(BanrisulPatterns.Digits44)]
        public string Danfe { get; set; } = "";
    }

    public class Rateio : IValidatableObject
    {
        [EnumDataType(typeof(CodigoRateio), ErrorMessage = "Codigo must be one of the defined CodigoRateio values.")]
        public CodigoRateio Codigo { get; set; }

        [EnumDataType(typeof(TipoValor), ErrorMessage = "TipoValor must be one of the defined TipoValor values.")]
        public TipoValor TipoValor { get; set; }

        [Required]
        [MinLength(1)]
        [MaxLength(3)]
        public List<BeneficiarioRateio> Beneficiarios { get; set; } = new List<BeneficiarioRateio>();

        /// <summary>
        /// SPEC: <see cref="Codigo"/> == <see cref="CodigoRateio.ValorCobradoPagador"/> requires
        /// <see cref="TipoValor"/> == <see cref="Types.TipoValor.Percentual"/>. The rest is INFERRED.
        /// </summary>
        /// <remarks>
        /// Deliberately does not copy the Santander sibling library's exact-equality rule for the shared
        /// amount ("sharing values must sum to exactly the nominal value"): here
        /// <see cref="CodigoRateio.ValorCobradoPagador"/> means "share of the amount charged to the payer",
        /// which is not the registered nominal value, so an exact-equality check does not transfer. The sum
        /// against <see cref="Titulo.ValorNominal"/> when <see cref="TipoValor"/> is
        /// <see cref="Types.TipoValor.Valor"/> uses <c>&lt;=</c> and lives on <see cref="Titulo"/> itself (it
        /// needs <see cref="Titulo.ValorNominal"/>, which this class cannot see).
        /// </remarks>
        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (Codigo == CodigoRateio.ValorCobradoPagador && TipoValor != TipoValor.Percentual)
            {
                results.Add(new ValidationResult(
                    "TipoValor must be Percentual when Codigo is ValorCobradoPagador.",
                    new[] { nameof(TipoValor) }));
            }

            // Same reason as Titulo.Validate's own null-conditional accesses: this runs unconditionally, so a
            // null list from the wire has to skip the rules below and let [Required] report it.
            if (Beneficiarios is null)
                return results;

            if (TipoValor == TipoValor.Valor)
            {
                for (int i = 0; i < Beneficiarios.Count; i++)
                {
                    if (!Beneficiarios[i].Valor.HasValue)
                        results.Add(new ValidationResult($"Beneficiarios[{i}].Valor is required when TipoValor is Valor.", new[] { $"Beneficiarios[{i}].Valor" }));
                    if (Beneficiarios[i].Percentual.HasValue)
                        results.Add(new ValidationResult($"Beneficiarios[{i}].Percentual must be unset when TipoValor is Valor.", new[] { $"Beneficiarios[{i}].Percentual" }));
                }
            }
            else if (TipoValor == TipoValor.Percentual)
            {
                for (int i = 0; i < Beneficiarios.Count; i++)
                {
                    if (!Beneficiarios[i].Percentual.HasValue)
                        results.Add(new ValidationResult($"Beneficiarios[{i}].Percentual is required when TipoValor is Percentual.", new[] { $"Beneficiarios[{i}].Percentual" }));
                    if (Beneficiarios[i].Valor.HasValue)
                        results.Add(new ValidationResult($"Beneficiarios[{i}].Valor must be unset when TipoValor is Percentual.", new[] { $"Beneficiarios[{i}].Valor" }));
                }
            }

            // INFERRED.
            decimal percentualSum = Beneficiarios.Sum(b => b.Percentual ?? 0m);
            if (percentualSum > 100m)
            {
                results.Add(new ValidationResult(
                    $"Beneficiarios percentuals must sum to at most 100, but summed to {percentualSum}.",
                    new[] { nameof(Beneficiarios) }));
            }

            // INFERRED.
            var duplicateCodes = Beneficiarios
                .Where(b => !string.IsNullOrEmpty(b.Codigo))
                .GroupBy(b => b.Codigo)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var codigo in duplicateCodes)
            {
                results.Add(new ValidationResult(
                    $"Beneficiario code '{codigo}' is used more than once; Beneficiarios codes must be distinct.",
                    new[] { nameof(Beneficiarios) }));
            }

            return results;
        }
    }

    public class BeneficiarioRateio
    {
        [Required]
        [RegularExpression(BanrisulPatterns.Digits13)]
        public string Codigo { get; set; } = "";

        /// <summary>
        /// CPF/CNPJ of the sharing beneficiary. Sent on the request and also echoed back by the bank, so
        /// this gets format-only validation, no <see cref="RequiredAttribute"/> — the "Com Rateio" request
        /// sends it, but nothing in the schema requires it.
        /// </summary>
        [RegularExpression(BanrisulPatterns.CpfOrCnpj)]
        public string? CpfCnpj { get; set; }

        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.MoneyMax)]
        [DecimalScale(2)]
        [JsonConverter(typeof(DecimalF2Converter))]
        public decimal? Valor { get; set; }

        // F3 case: Percentual is serialized by DecimalF3Converter ("F3"), so the scale is 3, not 2.
        [DecimalRange(BanrisulBounds.MoneyMin, BanrisulBounds.Percent3Max)]
        [DecimalScale(3)]
        [JsonConverter(typeof(DecimalF3Converter))]
        public decimal? Percentual { get; set; }

        [Range(0, 9999)]
        public int? Parcela { get; set; }

        /// <summary>
        /// Retornado pelo banco, mas também enviado no request "Com Rateio" - format-only, no
        /// <see cref="RequiredAttribute"/>.
        /// </summary>
        [EnumDataType(typeof(TipoPessoa), ErrorMessage = "TipoPessoa must be one of the defined TipoPessoa values.")]
        [JsonConverter(typeof(TipoPessoaConverter))]
        public TipoPessoa? TipoPessoa { get; set; }

        /// <summary>
        /// Retornado pelo banco, mas também enviado no request "Com Rateio" - format-only, no
        /// <see cref="RequiredAttribute"/>.
        /// </summary>
        [StringLength(60, MinimumLength = 1)]
        [RegularExpression(BanrisulPatterns.Text)]
        public string? Nome { get; set; }

        /// <summary>
        /// Retornado pelo banco, mas também enviado no request "Com Rateio" - format-only, no
        /// <see cref="RequiredAttribute"/>.
        /// </summary>
        [StringLength(60, MinimumLength = 1)]
        [RegularExpression(BanrisulPatterns.Text)]
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
        [EnumDataType(typeof(SimNao), ErrorMessage = "Autoriza must be one of the defined SimNao values.")]
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
