using BoletoForBanrisul.Core.Models.Types;
using System.ComponentModel.DataAnnotations;

namespace BoletoForBanrisul.Core.Validation
{
    /// <summary>
    /// CPF/CNPJ check-digit ("dígito verificador") math, kept in one small, easy-to-find place so it can be
    /// relaxed or replaced without hunting through the model classes that use it.
    /// </summary>
    public static class DocumentNumberValidator
    {
        /// <summary>
        /// Validates that <paramref name="documentNumber"/> has the length required by
        /// <paramref name="tipoPessoa"/> (11 digits for CPF, 14 for CNPJ) and passes its check-digit
        /// algorithm.
        /// </summary>
        /// <param name="tipoPessoa">The declared kind of document holder.</param>
        /// <param name="documentNumber">The document number, digits only or with punctuation.</param>
        /// <param name="memberName">The member path to attach to any reported error.</param>
        /// <returns>Zero or more <see cref="ValidationResult"/>s describing what is wrong.</returns>
        public static IEnumerable<ValidationResult> Validate(TipoPessoa tipoPessoa, string? documentNumber, string memberName)
        {
            string digits = OnlyDigits(documentNumber ?? "");
            string label = tipoPessoa == TipoPessoa.PessoaFisica ? "CPF" : "CNPJ";
            int expectedLength = tipoPessoa == TipoPessoa.PessoaFisica ? 11 : 14;

            if (digits.Length != expectedLength)
            {
                yield return new ValidationResult(
                    $"{memberName} must have {expectedLength} digits for {label}, but has {digits.Length}.",
                    new[] { memberName });
                yield break;
            }

            bool valid = tipoPessoa == TipoPessoa.PessoaFisica ? IsValidCpf(digits) : IsValidCnpj(digits);
            if (!valid)
            {
                yield return new ValidationResult(
                    $"{memberName} is not a valid {label} number (check digit mismatch).",
                    new[] { memberName });
            }
        }

        /// <summary>Validates the two check digits of an 11-digit CPF.</summary>
        /// <param name="cpf">Exactly 11 digits, no punctuation.</param>
        public static bool IsValidCpf(string cpf)
        {
            if (string.IsNullOrEmpty(cpf) || cpf.Length != 11 || !AllDigits(cpf))
                return false;

            // Every digit the same (e.g. "11111111111") passes the weighted sum by construction but is
            // never a real CPF; the Federal Revenue explicitly rejects these.
            if (AllSameDigit(cpf))
                return false;

            int firstCheck = ComputeMod11Digit(cpf.Substring(0, 9), new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2 });
            int secondCheck = ComputeMod11Digit(cpf.Substring(0, 9) + firstCheck, new[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 });

            return cpf[9] - '0' == firstCheck && cpf[10] - '0' == secondCheck;
        }

        /// <summary>Validates the two check digits of a 14-digit CNPJ.</summary>
        /// <param name="cnpj">Exactly 14 digits, no punctuation.</param>
        public static bool IsValidCnpj(string cnpj)
        {
            if (string.IsNullOrEmpty(cnpj) || cnpj.Length != 14 || !AllDigits(cnpj))
                return false;

            if (AllSameDigit(cnpj))
                return false;

            int firstCheck = ComputeMod11Digit(cnpj.Substring(0, 12), new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 });
            int secondCheck = ComputeMod11Digit(cnpj.Substring(0, 12) + firstCheck, new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 });

            return cnpj[12] - '0' == firstCheck && cnpj[13] - '0' == secondCheck;
        }

        /// <summary>
        /// The shared CPF/CNPJ check-digit recipe: multiply each digit by its weight, sum, take the
        /// remainder mod 11, and fold remainders of 0 or 1 down to 0.
        /// </summary>
        private static int ComputeMod11Digit(string digits, int[] weights)
        {
            int sum = 0;
            for (int i = 0; i < digits.Length; i++)
                sum += (digits[i] - '0') * weights[i];

            int remainder = sum % 11;
            return remainder < 2 ? 0 : 11 - remainder;
        }

        private static bool AllDigits(string value)
        {
            foreach (char c in value)
                if (c < '0' || c > '9')
                    return false;

            return true;
        }

        private static bool AllSameDigit(string value)
        {
            for (int i = 1; i < value.Length; i++)
                if (value[i] != value[0])
                    return false;

            return true;
        }

        /// <summary>
        /// Strips everything but digits. Deliberately not a call to <c>BoletoHelper.OnlyNumbers</c>: that
        /// helper is <c>private</c> in this library, and widening it just to reach it from here would grow
        /// its public surface for a five-line filter that has no reason to live anywhere else.
        /// </summary>
        private static string OnlyDigits(string text)
        {
            var chars = new char[text.Length];
            int count = 0;
            foreach (char c in text)
            {
                if (c >= '0' && c <= '9')
                    chars[count++] = c;
            }

            return new string(chars, 0, count);
        }
    }
}
