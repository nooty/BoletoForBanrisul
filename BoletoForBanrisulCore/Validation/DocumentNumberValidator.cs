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
        /// <paramref name="tipoPessoa"/> (11 digits for CPF, 14 characters for CNPJ) and passes its
        /// check-digit algorithm.
        /// </summary>
        /// <param name="tipoPessoa">The declared kind of document holder.</param>
        /// <param name="documentNumber">The document number, optionally with punctuation.</param>
        /// <param name="memberName">The member path to attach to any reported error.</param>
        /// <returns>Zero or more <see cref="ValidationResult"/>s describing what is wrong.</returns>
        public static IEnumerable<ValidationResult> Validate(TipoPessoa tipoPessoa, string? documentNumber, string memberName)
        {
            string normalized = tipoPessoa == TipoPessoa.PessoaFisica
                ? OnlyDigits(documentNumber ?? "")
                : OnlyAsciiLettersAndDigits(documentNumber ?? "");
            string label = tipoPessoa == TipoPessoa.PessoaFisica ? "CPF" : "CNPJ";
            int expectedLength = tipoPessoa == TipoPessoa.PessoaFisica ? 11 : 14;
            string unit = tipoPessoa == TipoPessoa.PessoaFisica ? "digits" : "characters";

            if (normalized.Length != expectedLength)
            {
                yield return new ValidationResult(
                    $"{memberName} must have {expectedLength} {unit} for {label}, but has {normalized.Length}.",
                    new[] { memberName });
                yield break;
            }

            bool valid = tipoPessoa == TipoPessoa.PessoaFisica ? IsValidCpf(normalized) : IsValidCnpj(normalized);
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

        /// <summary>Validates the two numeric check digits of a 14-character alphanumeric CNPJ.</summary>
        /// <param name="cnpj">Exactly 12 uppercase ASCII letters or digits followed by 2 digits, with no punctuation.</param>
        public static bool IsValidCnpj(string cnpj)
        {
            if (string.IsNullOrEmpty(cnpj) || cnpj.Length != 14 ||
                !AllUppercaseAsciiLettersOrDigits(cnpj.Substring(0, 12)) ||
                !AllDigits(cnpj.Substring(12, 2)))
                return false;

            if (AllSameDigit(cnpj))
                return false;

            int firstCheck = ComputeMod11Digit(cnpj.Substring(0, 12), new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 });
            int secondCheck = ComputeMod11Digit(cnpj.Substring(0, 12) + firstCheck, new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 });

            return cnpj[12] - '0' == firstCheck && cnpj[13] - '0' == secondCheck;
        }

        /// <summary>
        /// The shared CPF/CNPJ check-digit recipe: convert each character to its ASCII code minus 48,
        /// multiply it by its weight, sum, take the remainder mod 11, and fold remainders of 0 or 1
        /// down to 0. For decimal digits, the conversion is the usual numeric value.
        /// </summary>
        private static int ComputeMod11Digit(string characters, int[] weights)
        {
            int sum = 0;
            for (int i = 0; i < characters.Length; i++)
                sum += (characters[i] - '0') * weights[i];

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

        private static bool AllUppercaseAsciiLettersOrDigits(string value)
        {
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z')))
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

        /// <summary>Strips CNPJ punctuation while preserving the uppercase alphanumeric base and check digits.</summary>
        private static string OnlyAsciiLettersAndDigits(string text)
        {
            var chars = new char[text.Length];
            int count = 0;
            foreach (char c in text)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    chars[count++] = c;
            }

            return new string(chars, 0, count);
        }
    }
}
