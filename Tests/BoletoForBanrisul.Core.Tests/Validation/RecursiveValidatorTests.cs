using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using BoletoForBanrisul.Core.Validation;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BoletoForBanrisul.Core.Tests.Validation
{
    /// <summary>
    /// The recursive validator exists because <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>
    /// neither descends into complex properties nor into collections, and because it stops reporting once a
    /// node's attributes fail. Every test here is about that contract: find everything, and say where.
    /// </summary>
    [Trait("Category", "Unit")]
    public class RecursiveValidatorTests
    {
        private static string[] PathsOf(IEnumerable<ValidationResult> results) =>
            results.SelectMany(r => r.MemberNames).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        /// <summary>
        /// The errors from one validation pass, for the tests that assert on what was found rather than on
        /// the pass/fail outcome. The outcome itself is covered by the two TryValidate_Returns tests below.
        /// </summary>
        private static IReadOnlyList<ValidationResult> ErrorsOf(object? instance)
        {
            RecursiveValidator.TryValidate(instance, out var errors);
            return errors;
        }

        [Fact]
        public void TryValidate_ReturnsTrueAndNoErrors_ForAValidGraph()
        {
            // The Try-pattern contract: the return value is the verdict, so a caller can branch on it
            // without inspecting the collection.
            Assert.True(RecursiveValidator.TryValidate(BoletoFactory.CreateValid(), out var errors));
            Assert.Empty(errors);
        }

        [Fact]
        public void TryValidate_ReturnsFalseAndTheErrors_ForAnInvalidGraph()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Pagador.Cep = "123";

            Assert.False(RecursiveValidator.TryValidate(titulo, out var errors));
            Assert.Equal("Pagador.Cep", Assert.Single(Assert.Single(errors).MemberNames));
        }

        [Fact]
        public void FullyPopulatedValidBoleto_ProducesNoErrors()
        {
            var errors = ErrorsOf(BoletoFactory.CreateValid());

            Assert.Empty(errors);
        }

        [Fact]
        public void MinimalValidBoleto_ProducesNoErrors()
        {
            var errors = ErrorsOf(BoletoFactory.CreateMinimal());

            Assert.Empty(errors);
        }

        [Fact]
        public void Null_ProducesNoErrors()
        {
            Assert.Empty(ErrorsOf(null));
        }

        [Fact]
        public void SimultaneousFaultsAtDifferentDepths_AreAllReportedInOnePass()
        {
            // The point of the validator: a caller fixing a body should see every problem at once rather
            // than discovering them one round-trip at a time. Validating the full Boleto (rather than a bare
            // Titulo, as most other tests in this file do) is deliberate here: it is what exercises the
            // "Titulo." prefix a real request actually carries at every depth below it.
            var boleto = new Boleto { Titulo = BoletoFactory.CreateValid() };
            boleto.Titulo.Pagador.Cep = "123";                                // attribute, two levels down
            boleto.Titulo.Pagador.CpfCnpj = BoletoFactory.PlaceholderCpf;     // IValidatableObject, two levels down
            boleto.Titulo.DataVencimento = boleto.Titulo.DataEmissao.AddDays(-1); // cross-field, one level down
            // The discount date window ([DataEmissao, DataVencimento]) is checked against the very
            // DataVencimento faulted above, so it now spans no days at all and Desconto.Data fails no matter
            // what it is set to - that is this test's "three levels down" entry, riding along with the
            // due-date fault rather than needing a separate injected value.
            // Four entries trips [MaxLength(3)] on Rateio.Beneficiarios; codes are distinct and the values
            // still sum to at most ValorNominal (150.50) so the sharing-vs-nominal rule stays quiet and only
            // the collection-length attribute fires.
            boleto.Titulo.Rateio!.Beneficiarios = new List<BeneficiarioRateio>
            {
                new BeneficiarioRateio { Codigo = "0000000000001", Valor = 10m },
                new BeneficiarioRateio { Codigo = "0000000000002", Valor = 10m },
                new BeneficiarioRateio { Codigo = "0000000000003", Valor = 10m },
                new BeneficiarioRateio { Codigo = "0000000000004", Valor = 10m }
            };

            var errors = ErrorsOf(boleto);
            var paths = PathsOf(errors);

            Assert.Equal(
                new[]
                {
                    "Titulo.DataVencimento",
                    "Titulo.Instrucoes.Desconto.Data",
                    "Titulo.Pagador.Cep",
                    "Titulo.Pagador.CpfCnpj",
                    "Titulo.Rateio.Beneficiarios"
                },
                paths);
        }

        [Fact]
        public void ValidateAndThrow_CarriesEveryError()
        {
            var titulo = BoletoFactory.CreateValid();
            titulo.Pagador.Cep = "123";
            // Rateio/Abatimento are tied to ValorNominal by their own rules; nulled out so dropping
            // ValorNominal to zero triggers only the one rule this test means to combine with Pagador.Cep.
            titulo.Rateio = null;
            titulo.Instrucoes.Abatimento = null;
            titulo.ValorNominal = 0m;

            var exception = Assert.Throws<ModelValidationException>(() => RecursiveValidator.ValidateAndThrow(titulo));

            Assert.Equal(2, exception.Errors.Count);
            Assert.Contains("Pagador.Cep", PathsOf(exception.Errors));
            Assert.Contains(nameof(Titulo.ValorNominal), PathsOf(exception.Errors));
        }

        [Fact]
        public void ValidateAndThrow_DoesNotThrowForAValidBoleto()
        {
            RecursiveValidator.ValidateAndThrow(BoletoFactory.CreateValid());
        }

        [Fact]
        public void MultiMemberRule_IsReportedOncePerMemberItBlames()
        {
            // A rule can blame more than one property. Errors are keyed by member path, so such a rule
            // arrives as one result per member, each naming exactly one member and repeating the shared
            // message. That is the shape a caller mapping errors onto form fields needs.
            var titulo = BoletoFactory.CreateMinimal();
            titulo.PagParcial.Tipo = TipoValor.Valor;
            titulo.PagParcial.ValorMin = 90m;
            titulo.PagParcial.ValorMax = 10m;

            var errors = ErrorsOf(titulo);

            Assert.Equal(new[] { "PagParcial.ValorMax", "PagParcial.ValorMin" }, PathsOf(errors));
            Assert.All(errors, e => Assert.Single(e.MemberNames));
            Assert.Single(errors.Select(e => e.ErrorMessage).Distinct());
        }

        [Fact]
        public void NoMemberName_ContainsASeparatorCharacter()
        {
            var titulo = BoletoFactory.CreateMinimal();
            titulo.PagParcial.Tipo = TipoValor.Valor;
            titulo.PagParcial.ValorMin = 500m;
            titulo.PagParcial.ValorMax = 100m;

            var errors = ErrorsOf(titulo);

            Assert.NotEmpty(errors);
            Assert.All(errors, e => Assert.All(e.MemberNames, n => Assert.DoesNotContain(";", n)));
        }

        [Fact]
        public void SharedInstance_IsReportedAtItsFirstPositionOnly()
        {
            var invalid = new BeneficiarioRateio { Codigo = "1", Valor = 1m }; // "1" fails the 13-digit Codigo pattern
            var titulo = BoletoFactory.CreateMinimal();
            titulo.Rateio = new Rateio
            {
                Codigo = CodigoRateio.ValorRegistradoTitulo,
                TipoValor = TipoValor.Valor,
                Beneficiarios = new List<BeneficiarioRateio> { invalid, invalid }
            };

            var paths = PathsOf(ErrorsOf(titulo));

            // Nodes already validated in this pass are not revisited, so the same instance in two positions
            // is reported at the first one only. The caller is not left unaware of the second entry either:
            // sharing the instance necessarily duplicates the code, which trips the distinctness rule at
            // "Rateio.Beneficiarios".
            Assert.Contains("Rateio.Beneficiarios[0].Codigo", paths);
            Assert.Contains("Rateio.Beneficiarios", paths);
            Assert.DoesNotContain("Rateio.Beneficiarios[1].Codigo", paths);
        }

        [Fact]
        public void CyclicGraph_TerminatesAndReportsEachNodeOnce()
        {
            var first = new CycleNode();
            var second = new CycleNode();
            first.Next = second;
            second.Next = first;

            var errors = ErrorsOf(first);

            Assert.Equal(new[] { "Name", "Next.Name" }, PathsOf(errors));
        }

        [Fact]
        public void CollectionElements_AreEachReportedWithTheirIndex()
        {
            // Wrapped in a Boleto (rather than a bare Titulo) to also pin the indexed form's "Titulo." prefix.
            var boleto = new Boleto { Titulo = BoletoFactory.CreateMinimal() };
            // Values sum to 100, within ValorNominal (150.50, from CreateMinimal), so the sharing-vs-nominal
            // rule stays quiet and only the malformed Codigo on the last two entries is reported.
            boleto.Titulo.Rateio = new Rateio
            {
                Codigo = CodigoRateio.ValorRegistradoTitulo,
                TipoValor = TipoValor.Valor,
                Beneficiarios = new List<BeneficiarioRateio>
                {
                    new BeneficiarioRateio { Codigo = "0000000000001", Valor = 1m },
                    new BeneficiarioRateio { Codigo = "x", Valor = 1m },
                    new BeneficiarioRateio { Codigo = "003", Valor = 98m }
                }
            };

            var paths = PathsOf(ErrorsOf(boleto));

            Assert.Equal(new[] { "Titulo.Rateio.Beneficiarios[1].Codigo", "Titulo.Rateio.Beneficiarios[2].Codigo" }, paths);
        }

        [Fact]
        public void DecimalRangeAttribute_IsEvaluatedUnderACommaDecimalCulture()
        {
            // RangeAttribute parses its string bounds with CurrentCulture, so "999999999999.99" throws
            // rather than validating under pt-BR unless ParseLimitsInInvariantCulture is set. pt-BR is the
            // expected locale for this library's users, so the failure would land squarely on them.
            // DecimalRangeAttribute exists precisely so this does not happen.
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

                Assert.Empty(ErrorsOf(BoletoFactory.CreateValid()));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void FailingAttribute_DoesNotSuppressTheObjectsCustomRule()
        {
            // Validator.TryValidateObject skips IValidatableObject.Validate() when an attribute on the same
            // object already failed. The walker runs the two as independent steps so both are reported.
            var titulo = BoletoFactory.CreateValid();
            titulo.Pagador.Cep = "123";                              // attribute on Pagador
            titulo.Pagador.CpfCnpj = BoletoFactory.PlaceholderCpf;   // Pagador's IValidatableObject rule

            var paths = PathsOf(ErrorsOf(titulo));

            Assert.Contains("Pagador.Cep", paths);
            Assert.Contains("Pagador.CpfCnpj", paths);
        }

        [Fact]
        public void CustomRuleFailingAlone_IsReportedExactlyOnce()
        {
            // The mirror of the case above: with the object's attributes clean, the custom rule must not be
            // reported twice.
            var titulo = BoletoFactory.CreateValid();
            titulo.Instrucoes.Desconto!.Data = titulo.DataEmissao.AddDays(-1);

            var errors = ErrorsOf(titulo);

            Assert.Single(errors);
            Assert.Equal("Instrucoes.Desconto.Data", Assert.Single(errors[0].MemberNames));
        }

        [Fact]
        public void RootClassLevelError_IsReportedUnderAPlaceholderPath()
        {
            // A rule blaming no particular member has no member path of its own at the root. Passing the
            // empty name straight through would make the error invisible to a caller grouping by path, so
            // it gets an explicit placeholder. Nested objects need no such thing: their class-level errors
            // are keyed by the path of the object itself, as CollectionElements/CyclicGraph cover.
            var errors = ErrorsOf(new ObjectLevelRule());

            var error = Assert.Single(errors);
            Assert.Equal("(object)", Assert.Single(error.MemberNames));
        }

        private sealed class ObjectLevelRule : IValidatableObject
        {
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                yield return new ValidationResult("The object as a whole is wrong.");
            }
        }

        private sealed class CycleNode
        {
            [Required]
            public string? Name { get; set; }

            public CycleNode? Next { get; set; }
        }
    }
}
