using BoletoForBanrisul.Core.Models.Types;
using BoletoForBanrisul.Core.Validation;

namespace BoletoForBanrisul.Core.Tests.Validation
{
    /// <summary>CPF/CNPJ check digits.</summary>
    /// <remarks>
    /// Banrisul's 1.6.1 contract accepts an 11-digit CPF or a 14-character uppercase alphanumeric CNPJ.
    /// The bank's pattern does not validate check digits, so that maths remains a library-side net that
    /// turns a typo into a local error instead of a rejected registration. It lives in one isolated class
    /// so the extra validation can be changed without altering the transport contract.
    /// </remarks>
    [Trait("Category", "Unit")]
    public class DocumentNumberValidatorTests
    {
        [Theory]
        [InlineData(BoletoFactory.ValidCpf)]         // Pagador, from the bank's sandbox samples
        [InlineData(BoletoFactory.ValidSacadorCpf)]  // Sacador, from the bank's "Com Sacador" sample
        [InlineData("67524451091")]                  // from BoletoGenerator.GenerateBoleto1
        [InlineData("52998224725")]
        public void IsValidCpf_AcceptsNumbersWithCorrectCheckDigits(string cpf)
        {
            Assert.True(DocumentNumberValidator.IsValidCpf(cpf));
        }

        [Theory]
        [InlineData(BoletoFactory.PlaceholderCpf)] // repeated digits satisfy the sum but are never issued
        [InlineData("00000000000")]
        [InlineData("00000000192")]                // ValidCpf with its last digit altered
        [InlineData("0000000019")]                  // too short
        [InlineData("000000001911")]                // too long
        [InlineData("0000000019a")]
        [InlineData("")]
        public void IsValidCpf_RejectsEverythingElse(string cpf)
        {
            Assert.False(DocumentNumberValidator.IsValidCpf(cpf));
        }

        [Theory]
        [InlineData(BoletoFactory.ValidCnpj)] // from the bank's "Com Rateio" sample
        [InlineData(BoletoFactory.ValidAlphanumericCnpj)] // Federal Revenue's official alphanumeric example
        [InlineData("11222333000181")]
        public void IsValidCnpj_AcceptsDocumentsWithCorrectCheckDigits(string cnpj)
        {
            Assert.True(DocumentNumberValidator.IsValidCnpj(cnpj));
        }

        [Theory]
        [InlineData("92702067000197")] // ValidCnpj with its last digit altered
        [InlineData("12ABC34501DE36")] // alphanumeric CNPJ with its last digit altered
        [InlineData("12abc34501de35")] // the API contract requires uppercase letters
        [InlineData("12ABC34501DE3A")] // the two check digits remain numeric
        [InlineData("11111111111111")]
        [InlineData("9270206700019")]  // too short
        [InlineData("")]
        public void IsValidCnpj_RejectsEverythingElse(string cnpj)
        {
            Assert.False(DocumentNumberValidator.IsValidCnpj(cnpj));
        }

        [Fact]
        public void Validate_ReportsALengthMismatchBeforeTheCheckDigits()
        {
            // A CPF-length number declared as a CNPJ (PessoaJuridica) should be told it is the wrong length,
            // not that its check digits are wrong - the latter would send someone hunting for a typo that is
            // not there.
            var results = DocumentNumberValidator
                .Validate(TipoPessoa.PessoaJuridica, BoletoFactory.ValidCpf, "Pagador.CpfCnpj")
                .ToArray();

            var result = Assert.Single(results);
            Assert.Contains("must have 14 characters", result.ErrorMessage);
            Assert.Equal("Pagador.CpfCnpj", Assert.Single(result.MemberNames));
        }

        [Fact]
        public void Validate_ReportsACheckDigitMismatch()
        {
            var results = DocumentNumberValidator
                .Validate(TipoPessoa.PessoaFisica, BoletoFactory.PlaceholderCpf, "Pagador.CpfCnpj")
                .ToArray();

            var result = Assert.Single(results);
            Assert.Contains("check digit", result.ErrorMessage);
        }

        [Fact]
        public void Validate_RejectsAllSameDigit()
        {
            // 11111111111 satisfies the weighted check-digit sum by construction but was never issued; the
            // Federal Revenue explicitly rejects it, and so does this validator.
            Assert.False(DocumentNumberValidator.IsValidCpf(BoletoFactory.PlaceholderCpf));
        }

        [Fact]
        public void Validate_AcceptsAPunctuatedNumber()
        {
            // Punctuation is stripped before checking, so a caller pasting a formatted document is not
            // rejected for formatting alone. The [RegularExpression] on the model still requires the bare
            // document characters on the wire.
            Assert.Empty(DocumentNumberValidator.Validate(TipoPessoa.PessoaFisica, "000.000.001-91", "Pagador.CpfCnpj"));
        }

        [Fact]
        public void Validate_AcceptsAFormattedAlphanumericCnpj()
        {
            Assert.Empty(DocumentNumberValidator.Validate(
                TipoPessoa.PessoaJuridica,
                "12.ABC.345/01DE-35",
                "Pagador.CpfCnpj"));
        }

        [Fact]
        public void Validate_AcceptsTheBankSandboxDocuments()
        {
            Assert.Empty(DocumentNumberValidator.Validate(TipoPessoa.PessoaFisica, BoletoFactory.ValidCpf, "x"));
            Assert.Empty(DocumentNumberValidator.Validate(TipoPessoa.PessoaFisica, BoletoFactory.ValidSacadorCpf, "x"));
            Assert.Empty(DocumentNumberValidator.Validate(TipoPessoa.PessoaFisica, "67524451091", "x"));
            Assert.Empty(DocumentNumberValidator.Validate(TipoPessoa.PessoaJuridica, BoletoFactory.ValidCnpj, "x"));
            Assert.Empty(DocumentNumberValidator.Validate(TipoPessoa.PessoaJuridica, BoletoFactory.ValidAlphanumericCnpj, "x"));
        }
    }
}
