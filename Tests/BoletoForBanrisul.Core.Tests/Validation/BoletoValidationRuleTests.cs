using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using BoletoForBanrisul.Core.Validation;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BoletoForBanrisul.Core.Tests.Validation
{
    /// <summary>
    /// One test per validation rule on <see cref="Titulo"/> and its nested instructions: one accept, one
    /// reject, so a failure names the rule that broke rather than leaving a pile of errors to read through.
    /// Every rule exercised here traces back to a <c>SPEC</c>/<c>PARTIAL</c>/<c>INFERRED</c> comment in
    /// <c>Boleto.cs</c> - see that file for what each rule is actually for.
    /// </summary>
    [Trait("Category", "Unit")]
    public class BoletoValidationRuleTests
    {
        private static string[] PathsOf(IEnumerable<ValidationResult> results) =>
            results.SelectMany(r => r.MemberNames).ToArray();

        /// <summary>The errors from one validation pass; the pass/fail outcome is not what these tests assert.</summary>
        private static IReadOnlyList<ValidationResult> ErrorsOf(object? instance)
        {
            RecursiveValidator.TryValidate(instance, out var errors);
            return errors;
        }

        /// <summary>
        /// The single rule <paramref name="fault"/> is expected to violate, as one <see cref="ValidationResult"/>
        /// carrying every member that rule blames.
        /// </summary>
        /// <remarks>
        /// Errors are keyed by member path, so a rule blaming several members at once arrives as one result
        /// per member, all repeating the same message. Regrouping by message here restores "one rule, these
        /// members", which is what every test in this file is actually asserting.
        /// </remarks>
        private static ValidationResult SingleErrorFor(Action<Titulo> fault)
        {
            var titulo = BoletoFactory.CreateValid();
            fault(titulo);

            var errors = ErrorsOf(titulo);
            var rule = Assert.Single(errors.GroupBy(e => e.ErrorMessage));

            return new ValidationResult(rule.Key, rule.SelectMany(e => e.MemberNames).ToArray());
        }

        // --- Baseline shapes. ---

        [Fact]
        public void MinimalValidBoleto_ProducesNoErrors()
        {
            Assert.Empty(ErrorsOf(BoletoFactory.CreateMinimal()));
        }

        [Fact]
        public void FullyPopulatedValidBoleto_ProducesNoErrors()
        {
            Assert.Empty(ErrorsOf(BoletoFactory.CreateValid()));
        }

        [Fact]
        public void BankReturnedFieldsAreNotRequired_OnARequest()
        {
            // Titulo is shared between request and response (see its own <remarks>): a bank-returned ("BR")
            // property must never become required, because a query response that legitimately omits one of
            // them would then have no path that ever validates. This is the invariant that keeps Titulo
            // usable on both sides of the wire.
            var titulo = BoletoFactory.CreateMinimal();
            titulo.Beneficiario = null;
            titulo.CodigoBarras = null;
            titulo.LinhaDigitavel = null;
            titulo.SituacaoBanrisul = null;
            titulo.SituacaoCip = null;
            titulo.SituacaoPagamento = null;
            titulo.Carteira = null;
            titulo.Operacoes = null;

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void BankCreditCardPartialPaymentSample_IsAccepted()
        {
            // Regression guard, verbatim from the bank's own "Cartão de Crédito" success sample: codigo:1
            // (QualquerValor), tipo:2 (Valor), valor_min:1.50, with no valor_max at all. PagamentoParcial.Validate
            // deliberately does NOT require "both bounds set" or "Tipo only when Codigo is
            // ValorEntreMinimoEMaximo" - see its own <remarks> - precisely because every one of those rules
            // would reject this real, successful payload. If this test starts failing because someone
            // reintroduced one of those rules, revert the rule; do not loosen this test.
            var titulo = BoletoFactory.CreateMinimal();
            titulo.Especie = Especie.CartaoCredito;
            titulo.ValorNominal = 0m;
            titulo.PagParcial = new PagamentoParcial
            {
                Autoriza = AutorizacaoPagamentoParcial.Autoriza,
                Codigo = CodigoPagamentoParcial.QualquerValor,
                Quantidade = 17,
                Tipo = TipoValor.Valor,
                ValorMin = 1.50m
            };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rule 11 (INFERRED): ValorNominal must be positive unless Especie is CartaoCredito. ---

        [Fact]
        public void ZeroNominalValueForCreditCard_IsAccepted()
        {
            // The bank's own "Cartão de Crédito" sample registers "0.00".
            var titulo = BoletoFactory.CreateMinimal();
            titulo.Especie = Especie.CartaoCredito;
            titulo.ValorNominal = 0m;

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void ZeroNominalValueForOtherEspecie_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.Rateio = null;                    // tied to ValorNominal by rule 13; nulled to isolate the fault
                t.Instrucoes.Abatimento = null;      // tied to ValorNominal by rule 7; same reason
                t.ValorNominal = 0m;
            });

            Assert.Equal(nameof(Titulo.ValorNominal), Assert.Single(error.MemberNames));
        }

        [Fact]
        public void NegativeNominalValue_IsRejected()
        {
            // Not SingleErrorFor: a negative value trips both [DecimalRange] and the positive-value rule -
            // two genuinely different findings, both blaming the same member.
            var titulo = BoletoFactory.CreateValid();
            titulo.Rateio = null;
            titulo.Instrucoes.Abatimento = null;
            titulo.ValorNominal = -1m;

            var errors = ErrorsOf(titulo);

            Assert.NotEmpty(errors);
            Assert.All(errors, e => Assert.Equal(nameof(Titulo.ValorNominal), Assert.Single(e.MemberNames)));
        }

        // --- Decimal scale: a compared/serialized amount must agree with what the wire carries. ---

        [Fact]
        public void MultaTaxaWithTwoDecimals_IsRejected()
        {
            // Multa.Taxa is serialized by DecimalF1Converter ("F1"): scale 1, not 2.
            var error = SingleErrorFor(t => t.Instrucoes.Multa!.Taxa = 0.45m);

            Assert.Equal("Instrucoes.Multa.Taxa", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void RateioPercentualWithFourDecimals_IsRejected()
        {
            // BeneficiarioRateio.Percentual is serialized by DecimalF3Converter ("F3"): scale 3, not 4.
            var error = SingleErrorFor(t =>
            {
                t.Rateio!.TipoValor = TipoValor.Percentual;
                t.Rateio.Beneficiarios = new List<BeneficiarioRateio>
                {
                    new BeneficiarioRateio { Codigo = "0010000001088", Percentual = 16.2534m }
                };
            });

            Assert.Equal("Rateio.Beneficiarios[0].Percentual", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void ValorNominalWithThreeDecimals_IsRejected()
        {
            // ValorNominal is serialized by DecimalF2Converter ("F2"): scale 2, the common case.
            var error = SingleErrorFor(t =>
            {
                t.Rateio = null;
                t.Instrucoes.Abatimento = null;
                t.ValorNominal = 150.501m;
            });

            Assert.Equal(nameof(Titulo.ValorNominal), Assert.Single(error.MemberNames));
        }

        // --- Note (the "40 vs 45" correction): Sacador.Nome is capped at 40, matching Pagador.Nome. ---

        [Fact]
        public void SacadorNameOf41Chars_IsRejected()
        {
            var error = SingleErrorFor(t => t.Sacador!.Nome = new string('A', 41));

            Assert.Equal("Sacador.Nome", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void SacadorNameOfExactly40Chars_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Sacador!.Nome = new string('A', 40);

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rule 8 (SPEC): CodigoProtesto 7 (NaoNegativar) and 8 (NegativarDiasCorridos), from the v1.6.0 gap fix. ---

        [Theory]
        [InlineData(CodigoProtesto.NaoNegativar, null)]
        [InlineData(CodigoProtesto.NegativarDiasCorridos, 5)]
        public void ProtestCode7And8_AreAccepted(CodigoProtesto codigo, int? prazo)
        {
            var titulo = BoletoFactory.CreateMinimal();
            titulo.DataEmissao = DateTime.Today;
            titulo.DataVencimento = DateTime.Today.AddDays(30); // not yet due, so a non-zero Prazo just needs to be >= 3
            titulo.Instrucoes.Protesto = new Protesto { Codigo = codigo, Prazo = prazo };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Trap guard: [TruncateString(n)] and [StringLength(m)] must never disagree (dead truncation vs live rejection). ---

        [Fact]
        public void TruncateAndStringLengthAgree_ForEveryProperty()
        {
            var offenders = new List<string>();

            var modelTypes = typeof(Boleto).Assembly.GetTypes()
                .Where(t => t.Namespace == typeof(Boleto).Namespace);

            foreach (var type in modelTypes)
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    var truncate = property.GetCustomAttribute<TruncateStringAttribute>();
                    var stringLength = property.GetCustomAttribute<StringLengthAttribute>();
                    if (truncate is null || stringLength is null)
                        continue;

                    int truncateLength = (int)typeof(TruncateStringAttribute)
                        .GetField("_length", BindingFlags.NonPublic | BindingFlags.Instance)!
                        .GetValue(truncate)!;

                    if (truncateLength < stringLength.MaximumLength)
                    {
                        offenders.Add(
                            $"{type.Name}.{property.Name}: TruncateString({truncateLength}) < StringLength({stringLength.MaximumLength})");
                    }
                }
            }

            Assert.True(
                offenders.Count == 0,
                "TruncateString must never truncate below what StringLength already rejects - otherwise a " +
                "value the caller thinks was accepted is silently shortened on the wire. Offending: " +
                string.Join(", ", offenders));
        }

        // --- Rules 1-2 (SPEC): DataVencimento/DataEmissao are required. ---

        [Fact]
        public void MissingDueDate_IsRejected()
        {
            // Not SingleErrorFor: default(DateTime) (year 1) is also "earlier than DataEmissao" (rule 3) and
            // pulls Instrucoes.Desconto.Data outside its now-collapsed window (rule 4), so three different
            // rules fire at once. This only pins that DataVencimento is required, not how many findings.
            var titulo = BoletoFactory.CreateValid();
            titulo.DataVencimento = default;

            var paths = PathsOf(ErrorsOf(titulo));

            Assert.Contains(nameof(Titulo.DataVencimento), paths);
        }

        [Fact]
        public void MissingIssueDate_IsRejected()
        {
            var error = SingleErrorFor(t => t.DataEmissao = default);

            Assert.Equal(nameof(Titulo.DataEmissao), Assert.Single(error.MemberNames));
        }

        [Fact]
        public void PresentDueAndIssueDates_AreAccepted()
        {
            Assert.Empty(ErrorsOf(BoletoFactory.CreateValid()));
        }

        // --- Rule 3 (SPEC): DataVencimento must not be earlier than DataEmissao. ---

        [Fact]
        public void DueDateBeforeIssueDate_IsRejected()
        {
            // Discount's date window is tied to both dates (rule 4); nulling it out isolates this fault.
            var error = SingleErrorFor(t =>
            {
                t.Instrucoes.Desconto = null;
                t.DataVencimento = t.DataEmissao.AddDays(-1);
            });

            Assert.Equal(nameof(Titulo.DataVencimento), Assert.Single(error.MemberNames));
        }

        [Fact]
        public void DueDateEqualToIssueDate_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Desconto = null;
            titulo.Instrucoes.Multa = null;    // Multa.Data must stay after the (now much earlier) DataVencimento
            titulo.DataVencimento = titulo.DataEmissao;

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rule 4 (SPEC): Instrucoes.Desconto.Data must be within [DataEmissao, DataVencimento]. ---

        [Fact]
        public void DiscountDateBeforeIssueDate_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Desconto!.Data = t.DataEmissao.AddDays(-1));

            Assert.Equal("Instrucoes.Desconto.Data", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void DiscountDateAfterDueDate_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Desconto!.Data = t.DataVencimento.AddDays(1));

            Assert.Equal("Instrucoes.Desconto.Data", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void DiscountDateAtEitherWindowBoundary_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Desconto!.Data = titulo.DataEmissao;
            Assert.Empty(ErrorsOf(titulo));

            titulo.Instrucoes.Desconto!.Data = titulo.DataVencimento;
            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rule 5 (SPEC): Instrucoes.Juros.Data must be strictly later than DataVencimento. ---

        [Fact]
        public void InterestDateEqualToDueDate_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Juros.Data = t.DataVencimento);

            Assert.Equal("Instrucoes.Juros.Data", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void InterestDateAfterDueDate_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Juros.Data = titulo.DataVencimento.AddDays(1);

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rule 6 (SPEC): Instrucoes.Multa.Data must be strictly later than DataVencimento. ---

        [Fact]
        public void FineDateEqualToDueDate_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Multa!.Data = t.DataVencimento);

            Assert.Equal("Instrucoes.Multa.Data", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void FineDateAfterDueDate_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Multa!.Data = titulo.DataVencimento.AddDays(1);

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rule 7 (PARTIAL): Instrucoes.Abatimento.Valor must not exceed ValorNominal. ---

        [Fact]
        public void AbatementGreaterThanNominalValue_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Abatimento!.Valor = t.ValorNominal + 0.01m);

            Assert.Equal("Instrucoes.Abatimento.Valor", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void AbatementEqualToNominalValue_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Abatimento!.Valor = titulo.ValorNominal;

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rule 8 (SPEC): Instrucoes.Protesto.Prazo, required for ProtestarDiasCorridos/NegativarDiasCorridos. ---

        [Fact]
        public void ProtestWithoutADeadline_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Protesto = new Protesto
            {
                Codigo = CodigoProtesto.ProtestarDiasCorridos,
                Prazo = null
            });

            Assert.Equal("Instrucoes.Protesto.Prazo", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void ProtestDeadlineOfLessThanThreeOnABankSlipNotYetDue_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.Instrucoes.Multa = null;    // Multa.Data/Desconto.Data are tied to DataEmissao/DataVencimento
                t.Instrucoes.Desconto = null; // by their own rules; nulled so only the deadline fault fires
                t.DataEmissao = DateTime.Today;
                t.DataVencimento = DateTime.Today.AddDays(30);
                t.Instrucoes.Protesto = new Protesto { Codigo = CodigoProtesto.ProtestarDiasCorridos, Prazo = 2 };
            });

            Assert.Equal("Instrucoes.Protesto.Prazo", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void ProtestDeadlineOfAtLeastThreeOnABankSlipNotYetDue_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Multa = null;
            titulo.Instrucoes.Desconto = null;
            titulo.DataEmissao = DateTime.Today;
            titulo.DataVencimento = DateTime.Today.AddDays(30);
            titulo.Instrucoes.Protesto = new Protesto { Codigo = CodigoProtesto.ProtestarDiasCorridos, Prazo = 3 };

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void ImmediateProtestOnABankSlipNotYetDue_IsRejected()
        {
            // Prazo == 0 means "protest immediately", which only makes sense once the bank slip is already
            // overdue.
            var error = SingleErrorFor(t =>
            {
                t.Instrucoes.Multa = null;
                t.Instrucoes.Desconto = null;
                t.DataEmissao = DateTime.Today;
                t.DataVencimento = DateTime.Today.AddDays(30);
                t.Instrucoes.Protesto = new Protesto { Codigo = CodigoProtesto.ProtestarDiasCorridos, Prazo = 0 };
            });

            Assert.Equal("Instrucoes.Protesto.Prazo", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void ImmediateProtestOnABankSlipAlreadyPastDue_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.DataEmissao = DateTime.Today.AddDays(-60);
            titulo.DataVencimento = DateTime.Today.AddDays(-30);
            titulo.Instrucoes.Multa = null;    // Multa.Data must stay strictly after the now much-earlier DataVencimento
            titulo.Instrucoes.Desconto = null; // its date window would otherwise need to move with the dates too
            titulo.Instrucoes.Protesto = new Protesto { Codigo = CodigoProtesto.ProtestarDiasCorridos, Prazo = 0 };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rules 9-10 (SPEC): PagParcial.Autoriza must stay NaoAutoriza unless Especie is CartaoCredito, and always when Hibrido is set. ---

        [Fact]
        public void PartialPaymentAuthorizedForNonCreditCardEspecie_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.Hibrido = null; // isolate rule 9 from rule 10
                t.PagParcial.Autoriza = AutorizacaoPagamentoParcial.Autoriza;
                t.PagParcial.Quantidade = 1;
            });

            Assert.Equal("PagParcial.Autoriza", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void PartialPaymentAuthorizedForCreditCardEspecie_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Hibrido = null;
            titulo.Especie = Especie.CartaoCredito;
            titulo.PagParcial.Autoriza = AutorizacaoPagamentoParcial.Autoriza;
            titulo.PagParcial.Quantidade = 1;

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void PartialPaymentAuthorizedWithHibridoSet_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.Especie = Especie.CartaoCredito; // isolate rule 10 from rule 9
                t.PagParcial.Autoriza = AutorizacaoPagamentoParcial.Autoriza;
                t.PagParcial.Quantidade = 1;
                t.Hibrido = new Hibrido { Autoriza = SimNao.Sim };
            });

            Assert.Equal("PagParcial.Autoriza", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void PartialPaymentNotAuthorizedWithHibridoSet_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.PagParcial.Autoriza = AutorizacaoPagamentoParcial.NaoAutoriza;
            titulo.Hibrido = new Hibrido { Autoriza = SimNao.Sim };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rule 12 (INFERRED): Mensagens.Linha must be distinct. ---

        [Fact]
        public void DuplicateMessageLines_AreRejected()
        {
            var error = SingleErrorFor(t => t.Mensagens = new List<Mensagem>
            {
                new Mensagem { Linha = 1, Texto = "primeira linha" },
                new Mensagem { Linha = 1, Texto = "segunda linha" }
            });

            Assert.Equal(nameof(Titulo.Mensagens), Assert.Single(error.MemberNames));
        }

        [Fact]
        public void DistinctMessageLines_AreAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Mensagens = new List<Mensagem>
            {
                new Mensagem { Linha = 1, Texto = "primeira linha" },
                new Mensagem { Linha = 2, Texto = "segunda linha" }
            };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rule 13 (INFERRED): Rateio.Beneficiarios values must sum to at most ValorNominal, when TipoValor is Valor. ---

        [Fact]
        public void SharingValueSumGreaterThanNominalValue_IsRejected()
        {
            var error = SingleErrorFor(t => t.Rateio!.Beneficiarios = new List<BeneficiarioRateio>
            {
                new BeneficiarioRateio { Codigo = "0010000001088", Valor = t.ValorNominal + 0.01m }
            });

            Assert.Equal("Rateio.Beneficiarios", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void SharingValueSumEqualToNominalValue_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Rateio!.Beneficiarios = new List<BeneficiarioRateio>
            {
                new BeneficiarioRateio { Codigo = "0010000001088", Valor = titulo.ValorNominal }
            };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Juros.Validate (SPEC/INFERRED): ValorPorDia requires Valor (not Taxa); TaxaMensal requires Taxa (not Valor). ---

        [Fact]
        public void InterestValorPorDiaWithoutValor_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Juros = new Juros { Codigo = CodigoJuros.ValorPorDia });

            Assert.Equal("Instrucoes.Juros.Valor", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void InterestValorPorDiaWithValor_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Juros = new Juros { Codigo = CodigoJuros.ValorPorDia, Valor = 0.50m };

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void InterestTaxaMensalWithoutTaxa_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Juros = new Juros { Codigo = CodigoJuros.TaxaMensal });

            Assert.Equal("Instrucoes.Juros.Taxa", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void InterestTaxaMensalWithTaxa_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Juros = new Juros { Codigo = CodigoJuros.TaxaMensal, Taxa = 1.00m };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Multa.Validate (SPEC): ValorFixo requires Valor (not Taxa); Percentual requires Taxa (not Valor). ---

        [Fact]
        public void FineValorFixoWithoutValor_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Multa = new Multa
            {
                Codigo = CodigoMulta.ValorFixo,
                Data = t.DataVencimento.AddDays(1)
            });

            Assert.Equal("Instrucoes.Multa.Valor", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void FineValorFixoWithValor_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Multa = new Multa
            {
                Codigo = CodigoMulta.ValorFixo,
                Data = titulo.DataVencimento.AddDays(1),
                Valor = 5.00m
            };

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void FinePercentualWithoutTaxa_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Multa = new Multa
            {
                Codigo = CodigoMulta.Percentual,
                Data = t.DataVencimento.AddDays(1)
            });

            Assert.Equal("Instrucoes.Multa.Taxa", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void FinePercentualWithTaxa_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Multa = new Multa
            {
                Codigo = CodigoMulta.Percentual,
                Data = titulo.DataVencimento.AddDays(1),
                Taxa = 0.4m
            };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Desconto.Validate (SPEC): fixed-value codes require Valor; percentage codes require Taxa. ---

        [Fact]
        public void DiscountFixedValueCodeWithoutValor_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Desconto = new Desconto
            {
                Codigo = CodigoDesconto.ValorFixoAteData,
                Data = t.DataEmissao
            });

            Assert.Equal("Instrucoes.Desconto.Valor", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void DiscountFixedValueCodeWithValor_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Desconto = new Desconto
            {
                Codigo = CodigoDesconto.ValorPorAntecipacaoDiaCorrido,
                Data = titulo.DataEmissao,
                Valor = 1.00m
            };

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void DiscountPercentageCodeWithoutTaxa_IsRejected()
        {
            var error = SingleErrorFor(t => t.Instrucoes.Desconto = new Desconto
            {
                Codigo = CodigoDesconto.PercentualAteData,
                Data = t.DataEmissao
            });

            Assert.Equal("Instrucoes.Desconto.Taxa", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void DiscountPercentageCodeWithTaxa_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Desconto = new Desconto
            {
                Codigo = CodigoDesconto.PercentualSobreValorNominalDiaCorrido,
                Data = titulo.DataEmissao,
                Taxa = 2.50m
            };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- PagamentoParcial.Validate: Quantidade required when Autoriza is Autoriza. ---

        [Fact]
        public void PartialPaymentAuthorizedWithoutQuantidade_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.Hibrido = null;
                t.Especie = Especie.CartaoCredito;
                t.PagParcial.Autoriza = AutorizacaoPagamentoParcial.Autoriza;
                t.PagParcial.Quantidade = null;
            });

            Assert.Equal("PagParcial.Quantidade", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void PartialPaymentAuthorizedWithQuantidade_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Hibrido = null;
            titulo.Especie = Especie.CartaoCredito;
            titulo.PagParcial.Autoriza = AutorizacaoPagamentoParcial.Autoriza;
            titulo.PagParcial.Quantidade = 2;

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- PagamentoParcial.Validate: Tipo required when Codigo is ValorEntreMinimoEMaximo. ---

        [Fact]
        public void PartialPaymentValorEntreMinimoEMaximoWithoutTipo_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.PagParcial.Codigo = CodigoPagamentoParcial.ValorEntreMinimoEMaximo;
                t.PagParcial.Tipo = null;
            });

            Assert.Equal("PagParcial.Tipo", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void PartialPaymentValorEntreMinimoEMaximoWithTipo_IsAccepted()
        {
            // CreateValid already sets Codigo = ValorEntreMinimoEMaximo and Tipo = Valor.
            Assert.Empty(ErrorsOf(BoletoFactory.CreateValid()));
        }

        // --- PagamentoParcial.Validate: Tipo/bound mutual exclusion and ValorMin/Max, PercentualMin/Max ordering. ---

        [Fact]
        public void PartialPaymentPercentualBoundsSetWithTipoValor_IsRejected()
        {
            var error = SingleErrorFor(t => t.PagParcial.PercentualMin = 10m);

            Assert.Equal("PagParcial.PercentualMin", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void PartialPaymentValorBoundsSetWithTipoPercentual_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.PagParcial.Tipo = TipoValor.Percentual;
                t.PagParcial.ValorMin = null;
                t.PagParcial.ValorMax = null;
                t.PagParcial.PercentualMin = 10m;
                t.PagParcial.PercentualMax = 90m;
                t.PagParcial.ValorMin = 10m; // set again after switching to Percentual: must now be rejected
            });

            Assert.Equal("PagParcial.ValorMin", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void PartialPaymentMinGreaterThanMax_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.PagParcial.ValorMin = 90m;
                t.PagParcial.ValorMax = 10m;
            });

            Assert.Equal(2, error.MemberNames.Count());
        }

        [Fact]
        public void PartialPaymentMinNotGreaterThanMax_IsAccepted()
        {
            // CreateValid already sets ValorMin (10.00) <= ValorMax (90.00).
            Assert.Empty(ErrorsOf(BoletoFactory.CreateValid()));
        }

        // --- Rateio.Validate (SPEC): Codigo == ValorCobradoPagador requires TipoValor == Percentual. ---

        [Fact]
        public void SharingValorCobradoPagadorWithTipoValor_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.Rateio!.Codigo = CodigoRateio.ValorCobradoPagador;
                // TipoValor stays Valor (from CreateValid).
            });

            Assert.Equal("Rateio.TipoValor", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void SharingValorCobradoPagadorWithTipoPercentual_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Rateio!.Codigo = CodigoRateio.ValorCobradoPagador;
            titulo.Rateio.TipoValor = TipoValor.Percentual;
            titulo.Rateio.Beneficiarios = new List<BeneficiarioRateio>
            {
                new BeneficiarioRateio { Codigo = "0010000001088", Percentual = 50m },
                new BeneficiarioRateio { Codigo = "0010022145083", Percentual = 50m }
            };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rateio.Validate (INFERRED): TipoValor Valor/Percentual requires the matching field per beneficiary, and forbids the other. ---

        [Fact]
        public void SharingTipoValorWithoutBeneficiaryValor_IsRejected()
        {
            var error = SingleErrorFor(t => t.Rateio!.Beneficiarios = new List<BeneficiarioRateio>
            {
                new BeneficiarioRateio { Codigo = "0010000001088" }
            });

            Assert.Equal("Rateio.Beneficiarios[0].Valor", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void SharingTipoPercentualWithBeneficiaryValorSet_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.Rateio!.TipoValor = TipoValor.Percentual;
                t.Rateio.Beneficiarios = new List<BeneficiarioRateio>
                {
                    new BeneficiarioRateio { Codigo = "0010000001088", Percentual = 50m, Valor = 1m }
                };
            });

            Assert.Equal("Rateio.Beneficiarios[0].Valor", Assert.Single(error.MemberNames));
        }

        // --- Rateio.Validate (INFERRED): percentuals must sum to at most 100. ---

        [Fact]
        public void SharingPercentualSumGreaterThanOneHundred_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.Rateio!.TipoValor = TipoValor.Percentual;
                t.Rateio.Beneficiarios = new List<BeneficiarioRateio>
                {
                    new BeneficiarioRateio { Codigo = "0010000001088", Percentual = 60m },
                    new BeneficiarioRateio { Codigo = "0010022145083", Percentual = 60m }
                };
            });

            Assert.Equal("Rateio.Beneficiarios", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void SharingPercentualSumEqualToOneHundred_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Rateio!.TipoValor = TipoValor.Percentual;
            titulo.Rateio.Beneficiarios = new List<BeneficiarioRateio>
            {
                new BeneficiarioRateio { Codigo = "0010000001088", Percentual = 50m },
                new BeneficiarioRateio { Codigo = "0010022145083", Percentual = 50m }
            };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Rateio.Validate (INFERRED): beneficiary codes must be distinct. ---

        [Fact]
        public void DuplicateSharingBeneficiaryCodes_AreRejected()
        {
            // Values sum to ValorNominal so rule 13 stays quiet and only the duplicate-code rule fires.
            var error = SingleErrorFor(t => t.Rateio!.Beneficiarios = new List<BeneficiarioRateio>
            {
                new BeneficiarioRateio { Codigo = "0010000001088", Valor = 75.25m },
                new BeneficiarioRateio { Codigo = "0010000001088", Valor = 75.25m }
            });

            Assert.Equal("Rateio.Beneficiarios", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void DistinctSharingBeneficiaryCodes_AreAccepted()
        {
            // Pinned by BoletoFactory.CreateValid() itself, which already uses two distinct codes.
            Assert.Empty(ErrorsOf(BoletoFactory.CreateValid()));
        }

        // --- Pagador.Validate / Sacador.Validate: CPF/CNPJ check-digit cross-check. ---

        [Fact]
        public void PayerCpfDeclaredAsCnpjLength_IsRejected()
        {
            var error = SingleErrorFor(t =>
            {
                t.Pagador.TipoPessoa = TipoPessoa.PessoaJuridica;
                t.Pagador.CpfCnpj = BoletoFactory.ValidCpf; // 11 digits, but TipoPessoa now says CNPJ (14)
            });

            Assert.Equal("Pagador.CpfCnpj", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void PayerCpfWithABadCheckDigit_IsRejected()
        {
            var error = SingleErrorFor(t => t.Pagador.CpfCnpj = BoletoFactory.PlaceholderCpf);

            Assert.Equal("Pagador.CpfCnpj", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void PayerCpfMatchingItsDeclaredType_IsAccepted()
        {
            // Pinned by BoletoFactory.CreateValid() itself: PessoaFisica with an 11-digit, checksum-valid CPF.
            Assert.Empty(ErrorsOf(BoletoFactory.CreateValid()));
        }

        // --- Attribute-level: SeuNumero charset and length. ---

        [Fact]
        public void SeuNumeroWithADisallowedCharacter_IsRejected()
        {
            var error = SingleErrorFor(t => t.SeuNumero = "abc@123");

            Assert.Equal(nameof(Titulo.SeuNumero), Assert.Single(error.MemberNames));
        }

        [Fact]
        public void SeuNumeroOfFourteenChars_IsRejected()
        {
            var error = SingleErrorFor(t => t.SeuNumero = new string('A', 14));

            Assert.Equal(nameof(Titulo.SeuNumero), Assert.Single(error.MemberNames));
        }

        [Fact]
        public void SeuNumeroOfThirteenChars_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.SeuNumero = new string('A', 13);

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void EmptySeuNumero_IsRejected()
        {
            // Not SingleErrorFor: an empty string can trip more than one of [Required]/[StringLength]/
            // [RegularExpression] at once depending on how many of that property's attributes are evaluated
            // before the first failure, so this only pins that SeuNumero is blamed, not how many findings.
            var titulo = BoletoFactory.CreateValid();
            titulo.SeuNumero = "";

            var errors = ErrorsOf(titulo);

            Assert.NotEmpty(errors);
            Assert.All(errors, e => Assert.Equal(nameof(Titulo.SeuNumero), Assert.Single(e.MemberNames)));
        }

        // --- Attribute-level: NossoNumero is loose (request is looser than the response schema). ---

        [Theory]
        [InlineData(null)]
        [InlineData("264")]           // the bank's own "Homologado com nosso número" sample: 3 digits, not 10
        [InlineData("0000038806")]    // exactly 10 digits
        public void NossoNumero_IsAccepted(string? nossoNumero)
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.NossoNumero = nossoNumero;

            Assert.Empty(ErrorsOf(titulo));
        }

        [Theory]
        [InlineData("12345678901")] // 11 digits: over the request's own 10-digit cap
        [InlineData("26A")]          // not all-digit
        public void NossoNumero_IsRejected(string nossoNumero)
        {
            // Not SingleErrorFor: an 11-digit value trips both [StringLength(10)] and the [RegularExpression]
            // (which itself caps at 10 digits) at once, so this only pins that NossoNumero is blamed.
            var titulo = BoletoFactory.CreateValid();
            titulo.NossoNumero = nossoNumero;

            var errors = ErrorsOf(titulo);

            Assert.NotEmpty(errors);
            Assert.All(errors, e => Assert.Equal(nameof(Titulo.NossoNumero), Assert.Single(e.MemberNames)));
        }

        // --- Attribute-level: IdTituloEmpresa charset and length. ---

        [Fact]
        public void IdTituloEmpresaWithADisallowedCharacter_IsRejected()
        {
            var error = SingleErrorFor(t => t.IdTituloEmpresa = "abc@123");

            Assert.Equal(nameof(Titulo.IdTituloEmpresa), Assert.Single(error.MemberNames));
        }

        [Fact]
        public void IdTituloEmpresaOfTwentySixChars_IsRejected()
        {
            var error = SingleErrorFor(t => t.IdTituloEmpresa = new string('1', 26));

            Assert.Equal(nameof(Titulo.IdTituloEmpresa), Assert.Single(error.MemberNames));
        }

        [Fact]
        public void IdTituloEmpresaOfTwentyFiveChars_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.IdTituloEmpresa = new string('1', 25);

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Attribute-level: Pagador.Cep, exactly 8 digits. ---

        [Fact]
        public void CepWithSevenDigits_IsRejected()
        {
            var error = SingleErrorFor(t => t.Pagador.Cep = "9001026");

            Assert.Equal("Pagador.Cep", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void CepWithEightDigits_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Pagador.Cep = "90010260";

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Attribute-level: Pagador.Uf, exactly two uppercase letters. ---

        [Fact]
        public void UfInLowercase_IsRejected()
        {
            var error = SingleErrorFor(t => t.Pagador.Uf = "rs");

            Assert.Equal("Pagador.Uf", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void UfOfTwoUppercaseLetters_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Pagador.Uf = "SP";

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Attribute-level: Pagador.Cidade charset and length. ---

        [Fact]
        public void CidadeWithADisallowedCharacter_IsRejected()
        {
            // '@' is actually inside BanrisulPatterns' Text character class; '<' is not.
            var error = SingleErrorFor(t => t.Pagador.Cidade = "PORTO<ALEGRE");

            Assert.Equal("Pagador.Cidade", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void CidadeOfSixteenChars_IsRejected()
        {
            var error = SingleErrorFor(t => t.Pagador.Cidade = new string('A', 16));

            Assert.Equal("Pagador.Cidade", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void CidadeOfFifteenChars_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Pagador.Cidade = new string('A', 15);

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Attribute-level: Especie must be one of the enum's defined values. ---

        [Fact]
        public void EspecieOutOfTheDefinedSet_IsRejected()
        {
            var error = SingleErrorFor(t => t.Especie = (Especie)5);

            Assert.Equal(nameof(Titulo.Especie), Assert.Single(error.MemberNames));
        }

        // --- Attribute-level: NotaFiscal.Danfe, exactly 44 digits. ---

        [Fact]
        public void DanfeWithFortyThreeDigits_IsRejected()
        {
            var error = SingleErrorFor(t => t.NotasFiscais = new List<NotaFiscal>
            {
                new NotaFiscal { Danfe = new string('1', 43) }
            });

            Assert.Equal("NotasFiscais[0].Danfe", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void DanfeWithFortyFourDigits_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.NotasFiscais = new List<NotaFiscal> { new NotaFiscal { Danfe = new string('1', 44) } };

            Assert.Empty(ErrorsOf(titulo));
        }

        // --- Attribute-level: collection count bounds. ---

        [Fact]
        public void EmptyMensagens_IsRejected()
        {
            var error = SingleErrorFor(t => t.Mensagens = new List<Mensagem>());

            Assert.Equal(nameof(Titulo.Mensagens), Assert.Single(error.MemberNames));
        }

        [Fact]
        public void NullMensagens_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Mensagens = null;

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void MensagensOfNineLines_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Mensagens = Enumerable.Range(1, 9)
                .Select(i => new Mensagem { Linha = i, Texto = $"linha {i}" })
                .ToList();

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void MensagensOfTenLines_IsRejected()
        {
            // Not SingleErrorFor: Mensagem.Linha is itself capped to [1,9] (see its own [Range]), so a 10th
            // entry cannot avoid repeating one of the nine already in use. Both the collection-length
            // attribute and the distinct-lines rule (rule 12) fire together here; that coupling is inherent
            // to the domain, not a test bug, so this asserts both rather than pretending there is only one.
            var titulo = BoletoFactory.CreateValid();
            titulo.Mensagens = Enumerable.Range(1, 9)
                .Select(i => new Mensagem { Linha = i, Texto = $"linha {i}" })
                .Append(new Mensagem { Linha = 9, Texto = "linha extra" })
                .ToList();

            var paths = PathsOf(ErrorsOf(titulo));

            Assert.Contains(nameof(Titulo.Mensagens), paths);
        }

        [Fact]
        public void EmptyRateioBeneficiarios_IsRejected()
        {
            var error = SingleErrorFor(t => t.Rateio!.Beneficiarios = new List<BeneficiarioRateio>());

            Assert.Equal("Rateio.Beneficiarios", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void RateioBeneficiariosOfThree_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Rateio!.Beneficiarios = new List<BeneficiarioRateio>
            {
                new BeneficiarioRateio { Codigo = "0010000001081", Valor = 50.50m },
                new BeneficiarioRateio { Codigo = "0010000001082", Valor = 50.00m },
                new BeneficiarioRateio { Codigo = "0010000001083", Valor = 50.00m }
            };

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void RateioBeneficiariosOfFour_IsRejected()
        {
            var error = SingleErrorFor(t => t.Rateio!.Beneficiarios = new List<BeneficiarioRateio>
            {
                new BeneficiarioRateio { Codigo = "0010000001081", Valor = 30.50m },
                new BeneficiarioRateio { Codigo = "0010000001082", Valor = 30.00m },
                new BeneficiarioRateio { Codigo = "0010000001083", Valor = 30.00m },
                new BeneficiarioRateio { Codigo = "0010000001084", Valor = 30.00m }
            });

            Assert.Equal("Rateio.Beneficiarios", Assert.Single(error.MemberNames));
        }

        [Fact]
        public void EmptyNotasFiscais_IsRejected()
        {
            var error = SingleErrorFor(t => t.NotasFiscais = new List<NotaFiscal>());

            Assert.Equal(nameof(Titulo.NotasFiscais), Assert.Single(error.MemberNames));
        }

        [Fact]
        public void NullNotasFiscais_IsAccepted()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.NotasFiscais = null;

            Assert.Empty(ErrorsOf(titulo));
        }

        [Fact]
        public void NotasFiscaisOfThree_IsRejected()
        {
            var error = SingleErrorFor(t => t.NotasFiscais = new List<NotaFiscal>
            {
                new NotaFiscal { Danfe = new string('1', 44) },
                new NotaFiscal { Danfe = new string('2', 44) },
                new NotaFiscal { Danfe = new string('3', 44) }
            });

            Assert.Equal(nameof(Titulo.NotasFiscais), Assert.Single(error.MemberNames));
        }
    }
}
