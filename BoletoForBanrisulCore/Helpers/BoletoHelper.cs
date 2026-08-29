using BoletoForBanrisul.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BoletoForBanrisul.Core.Helpers
{
    public static class BoletoHelper
    {
        /// <summary>
        /// Computes the full nosso número (8 digits + 2 check digits).
        /// Left-pads with zeros when the base number has fewer than 8 digits.
        /// </summary>
        /// <param name="baseNumber">Base number for the calculation.</param>
        /// <returns>The full 10-digit nosso número.</returns>
        public static string GetOurNumberWithCheckDigits(string baseNumber)
        {
            string numbers = OnlyNumbers(baseNumber).PadLeft(8, '0').Substring(0, 8);

            var controlNumbers = GetDoubleCheckDigit(numbers);

            return $"{numbers}{controlNumbers}";
        }

        /// <summary>
        /// Builds the boleto's linha digitável.
        /// </summary>
        /// <param name="boleto">The boleto's data.</param>
        /// <returns>The linha digitável.</returns>
        public static string GetDigitableLine(Boleto boleto)
        {
            // 1. Build the full barcode.
            string barcode = GetBarcode(boleto);

            // 2. Fields of the linha digitável.

            // Field 1: 01-04 (bank + currency) + 20-24 (free field) + DV1
            string field1 = $"{barcode.Substring(0, 4)}{barcode.Substring(19, 5)}";
            field1 += GetCheckDigitMod10(field1);

            // Field 2: 25-34 (free field) + DV2
            string field2 = $"{barcode.Substring(24, 10)}";
            field2 += GetCheckDigitMod10(field2);

            // Field 3: 35-44 (free field) + DV3
            string field3 = $"{barcode.Substring(34, 10)}";
            field3 += GetCheckDigitMod10(field3);

            // Field 4: 5 (DAC)
            string field4 = barcode.Substring(4, 1);

            // Field 5: 6-9 (due date factor) + 10-19 (nominal value)
            string field5 = barcode.Substring(5, 14);

            // 3. Assemble the complete linha digitável.
            return $"{field1}{field2}{field3}{field4}{field5}";
        }

        /// <summary>
        /// Formats a linha digitável into groups of digits separated by dots and spaces.
        ///
        /// Ref: 04192.1AAAd1bb ACCCC.CCCNNd2bb NNNNN.N40XXd3bb V FFFF9999999999
        /// </summary>
        /// <param name="digitableLine">The unformatted linha digitável.</param>
        /// <returns>The formatted linha digitável.</returns>
        public static string FormatDigitableLine(string digitableLine)
        {
            string field1 = digitableLine.Substring(0, 5) + "." + digitableLine.Substring(5, 5);
            string field2 = digitableLine.Substring(10, 5) + "." + digitableLine.Substring(15, 6);
            string field3 = digitableLine.Substring(21, 5) + "." + digitableLine.Substring(26, 6);
            string field4 = digitableLine.Substring(32, 1);
            string field5 = digitableLine.Substring(33, 14);

            return $"{field1}  {field2}  {field3}  {field4}  {field5}";
        }

        /// <summary>
        /// Builds the boleto's barcode, following the FEBRABAN standard.
        /// </summary>
        /// <param name="boleto">The boleto's data.</param>
        /// <returns>The barcode.</returns>
        public static string GetBarcode(Boleto boleto)
        {
            // 01-03: bank code.
            string bankCode = "041";

            // 04: currency (9 for Real).
            string currency = "9";

            // 06-09: due date factor.
            string dueDateFactor = GetDueDateFactor(boleto.Titulo.DataVencimento);

            // 10-19: nominal value (no separator, 2 decimal places).
            string nominalValue = boleto.Titulo.ValorNominal > 0 ?
                ((long)Math.Round(boleto.Titulo.ValorNominal * 100m, MidpointRounding.AwayFromZero)).ToString().PadLeft(10, '0') : "0000000000";

            // 20-44: free field.
            var beneficiary = OnlyNumbers(boleto.Titulo.Beneficiario?.Codigo ?? "").PadLeft(13, '0').Substring(0, 13);

            var ourNumberBase = OnlyNumbers(boleto.Titulo.NossoNumero ?? "").PadLeft(8, '0');

            if(ourNumberBase.Length == 8)
                ourNumberBase = ourNumberBase + GetDoubleCheckDigit(ourNumberBase);

            var ourNumber = ourNumberBase.PadLeft(10, '0').Substring(0, 10);

            string freeField = GetFreeField(beneficiary, ourNumber);

            // Assemble the partial code (without the DAC yet).
            string partialCode = bankCode + currency + dueDateFactor + nominalValue + freeField;

            // 05: compute the DAC (self-check digit).
            string dac = GetDAC(partialCode).ToString();

            // Build the complete barcode.
            return bankCode + currency + dac + dueDateFactor + nominalValue + freeField;
        }

        /// <summary>
        /// Builds the barcode's free field from the beneficiário code and the nosso número.
        /// </summary>
        private static string GetFreeField(string beneficiaryCode, string ourNumber)
        {
            // Constant 2 at position 20 and constant 1 at position 21.
            string freeField = "21";

            // Positions 22-25: first 4 positions of the beneficiário code.
            freeField += beneficiaryCode.Substring(0, 4);

            // Positions 26-32: positions 5-11 of the beneficiário code (without the control digits).
            freeField += beneficiaryCode.Substring(4, 7);

            // Positions 33-40: nosso número (without the control digits).
            freeField += ourNumber.Substring(0, 8).PadLeft(8, '0');

            // Positions 41-42: constant 40.
            freeField += "40";

            // Positions 43-44: double check digit (mod 10 and mod 11).
            freeField += GetDoubleCheckDigit(freeField);

            return freeField;
        }

        /// <summary>
        /// Computes the double check digit from mod 10 and mod 11.
        /// </summary>
        private static string GetDoubleCheckDigit(string baseNumber)
        {
            // Mod 10.
            int dv1 = GetCheckDigitMod10(baseNumber);

            // Mod 11.
            string controlNumbers = GetCheckDigitsMod11(baseNumber + dv1);

            return controlNumbers.ToString();
        }

        /// <summary>
        /// Computes the check digit by mod 10.
        /// </summary>
        /// <param name="number">The base number used in the calculation.</param>
        /// <returns>The check digit computed by mod 10.</returns>
        private static int GetCheckDigitMod10(string number)
        {
            int[] weights = { 2, 1 };
            int sum = 0;

            for (int i = number.Length - 1, weightIndex = 0; i >= 0; i--, weightIndex = (weightIndex + 1) % 2)
            {
                int digit = int.Parse(number[i].ToString());
                int result = digit * weights[weightIndex];

                if (result > 9)
                    result -= 9;

                sum += result;
            }

            int remainder = sum % 10;
            return remainder == 0 ? 0 : 10 - remainder;
        }

        /// <summary>
        /// Computes the check digits by mod 11.
        /// </summary>
        /// <param name="number">The base number used in the calculation + DV1, computed by mod 10.</param>
        /// <returns>The check digits computed by mod 11.</returns>
        private static string GetCheckDigitsMod11(string number)
        {
            int[] weights = { 2, 3, 4, 5, 6, 7 };
            int sum = 0;
            int lastIndex = number.Length - 1;

            for (int i = number.Length - 1, weightIndex = 0; i >= 0; i--, weightIndex = (weightIndex + 1) % weights.Length)
            {
                int digit = int.Parse(number[i].ToString());
                sum += digit * weights[weightIndex];
            }

            int remainder = sum % 11;

            if (remainder == 0)
                return $"{number[lastIndex]}0";

            if (remainder == 1)
            {
                int dv1 = int.Parse(number[lastIndex].ToString()) + 1;

                if (dv1 > 9)
                    dv1 = 0;

                number = number.Substring(0, number.Length - 1) + dv1;

                return GetCheckDigitsMod11(number);
            }

            var controlNumber = 11 - remainder;
            return $"{number[lastIndex]}{controlNumber}";
        }

        /// <summary>
        /// Computes the due date factor from the due date.
        /// </summary>
        /// <param name="dueDate">The boleto's due date.</param>
        /// <returns>The due date factor as a 4-digit string.</returns>
        private static string GetDueDateFactor(DateTime dueDate)
        {
            DateTime baseDate = new DateTime(2000, 7, 3);
            int dayDifference = (int)(dueDate.Date - baseDate).TotalDays;

            // The due date factor itself.
            int factor = Math.Max((dayDifference % 9000) + 1000, 0);

            // Returned as a 4-digit string.
            return factor.ToString("D4");
        }

        /// <summary>
        /// Computes the DAC (self-check digit) using mod 11.
        /// </summary>
        /// <param name="partialCode">The boleto's partial code (without the DAC).</param>
        /// <returns>The computed DAC digit.</returns>
        private static int GetDAC(string partialCode)
        {
            int[] weights = { 2, 3, 4, 5, 6, 7, 8, 9 };
            int sum = 0;

            for (int i = partialCode.Length - 1, weightIndex = 0;
                i >= 0;
                i--, weightIndex = (weightIndex + 1) % weights.Length)
            {
                int digit = int.Parse(partialCode[i].ToString());
                sum += digit * weights[weightIndex];
            }

            int remainder = sum % 11;

            return (remainder == 0 || remainder == 1) ? 1 : (11 - remainder);
        }

        private static string OnlyNumbers(string text)
        {
            return new string(text.Where(char.IsDigit).ToArray());
        }
    }
}
