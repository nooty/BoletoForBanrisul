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
        /// Calcula o Nosso Número completo (8 dígitos + 2 dígitos verificadores).
        /// Completa com zeros à esquerda se o número base tiver menos de 8 dígitos.
        /// </summary>
        /// <param name="baseNumber">Número base para o cálculo.</param>
        /// <returns>Nosso Número completo com 10 dígitos.</returns>
        public static string GetOurNumberWithCheckDigits(string baseNumber)
        {
            string numbers = OnlyNumbers(baseNumber).PadLeft(8, '0').Substring(0, 8);

            var controlNumbers = GetDoubleCheckDigit(numbers);

            return $"{numbers}{controlNumbers}";
        }

        /// <summary>
        /// Gera a linha digitável do boleto.
        /// </summary>
        /// <param name="boleto">Dados do boleto.</param>
        /// <returns>Linha digitável.</returns>
        public static string GetDigitableLine(Boleto boleto)
        {
            // 1. Gerar o código de barras completo.
            string barcode = GetBarcode(boleto);

            // 2. Campos da linha digitável.

            // Campo 1: 01 a 04 (Banco + Moeda) + 20 a 24 (Campo Livre) + DV1
            string field1 = $"{barcode.Substring(0, 4)}{barcode.Substring(19, 5)}";
            field1 += GetCheckDigitMod10(field1);

            // Campo 2: 25 a 34 (Campo Livre) + DV2
            string field2 = $"{barcode.Substring(24, 10)}";
            field2 += GetCheckDigitMod10(field2);

            // Campo 3: 35 a 44 (Campo Livre) + DV3
            string field3 = $"{barcode.Substring(34, 10)}";
            field3 += GetCheckDigitMod10(field3);

            // Campo 4: 5 (DAC)
            string field4 = barcode.Substring(4, 1);

            // Campo 5: 6 a 9 (Fator de Vencimento) + 10 a 19 (Valor Nominal)
            string field5 = barcode.Substring(5, 14);

            // 3. Montar a linha digitável completa.
            return $"{field1}{field2}{field3}{field4}{field5}";
        }

        /// <summary>
        /// Formata uma linha digitável em grupos de números separados por pontos e espaços no formato correto.
        /// 
        /// Ref: 04192.1AAAd1bb ACCCC.CCCNNd2bb NNNNN.N40XXd3bb V FFFF9999999999
        /// </summary>
        /// <param name="digitableLine">Linha digitável sem formatação.</param>
        /// <returns>Linha digitável formatada.</returns>
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
        /// Gera o código de barras para o boleto, seguindo o padrão FEBRABAN.
        /// </summary>
        /// <param name="boleto">Dados do boleto.</param>
        /// <returns>Código de barras gerado.</returns>
        public static string GetBarcode(Boleto boleto)
        {
            // 01 a 03: Código do Banco.
            string bankCode = "041";

            // 04: Moeda (9 para Real).
            string currency = "9";

            // 06 a 09: Fator de Vencimento.
            string dueDateFactor = GetDueDateFactor(boleto.Titulo.DataVencimento);

            // 10 a 19: Valor Nominal (sem vírgula, com 2 casas decimais).
            string nominalValue = boleto.Titulo.ValorNominal > 0 ?
                ((long)Math.Round(boleto.Titulo.ValorNominal * 100m, MidpointRounding.AwayFromZero)).ToString().PadLeft(10, '0') : "0000000000";

            // 20 a 44: Campo Livre.
            var beneficiary = OnlyNumbers(boleto.Titulo.Beneficiario?.Codigo ?? "").PadLeft(13, '0').Substring(0, 13);

            var ourNumberBase = OnlyNumbers(boleto.Titulo.NossoNumero ?? "").PadLeft(8, '0');

            if(ourNumberBase.Length == 8)
                ourNumberBase = ourNumberBase + GetDoubleCheckDigit(ourNumberBase);

            var ourNumber = ourNumberBase.PadLeft(10, '0').Substring(0, 10);

            string freeField = GetFreeField(beneficiary, ourNumber);

            // Montar o código parcial (sem o DAC ainda).
            string partialCode = bankCode + currency + dueDateFactor + nominalValue + freeField;

            // 05: Calcular o DAC (dígito de autoconferência).
            string dac = GetDAC(partialCode).ToString();

            // Gerar o código de barras completo.
            return bankCode + currency + dac + dueDateFactor + nominalValue + freeField;
        }

        /// <summary>
        /// Gera o campo livre do código de barras com base no código do beneficiário e no nosso número.
        /// </summary>
        private static string GetFreeField(string beneficiaryCode, string ourNumber)
        {
            // Constante 2 nas posições 20 a 20 e Constante 1 nas posições 21 a 21.
            string freeField = "21";

            // Posições 22 a 25: 4 primeiras posições do Código de Beneficiário.
            freeField += beneficiaryCode.Substring(0, 4);

            // Posições 26 a 32: Posições 5 a 11 do Código de Beneficiário (sem controle).
            freeField += beneficiaryCode.Substring(4, 7);

            // Posições 33 a 40: Nosso Número (sem controle).
            freeField += ourNumber.Substring(0, 8).PadLeft(8, '0');

            // Posições 41 a 42: Constante 40.
            freeField += "40";

            // Posições 43 a 44: Duplo Dígito (módulo 10 e módulo 11).
            freeField += GetDoubleCheckDigit(freeField);

            return freeField;
        }

        /// <summary>
        /// Calcula o duplo dígito verificador com base nos módulos 10 e 11.
        /// </summary>
        private static string GetDoubleCheckDigit(string baseNumber)
        {
            // Módulo 10.
            int dv1 = GetCheckDigitMod10(baseNumber);

            // Módulo 11.
            string controlNumbers = GetCheckDigitsMod11(baseNumber + dv1);

            return controlNumbers.ToString();
        }

        /// <summary>
        /// Calcula o dígito verificador pelo módulo 10.
        /// </summary>
        /// <param name="number">O número base usado para o cálculo.</param>
        /// <returns>Dígito verificador calculado pelo módulo 10.</returns>
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
        /// Calcula os dígitos verificadores pelo módulo 11.
        /// </summary>
        /// <param name="number">O número base usado para o cálculo + DV1 calculado pelo módulo 10.</param>
        /// <returns>Dígitos verificadores calculados pelo módulo 11.</returns>
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
        /// Calcula o fator de vencimento com base na data de vencimento.
        /// </summary>
        /// <param name="dueDate">Data de vencimento do boleto.</param>
        /// <returns>Fator de vencimento como string de 4 dígitos.</returns>
        private static string GetDueDateFactor(DateTime dueDate)
        {
            DateTime baseDate = new DateTime(2000, 7, 3);
            int dayDifference = (int)(dueDate.Date - baseDate).TotalDays;

            // Cálculo do fator de vencimento.
            int factor = Math.Max((dayDifference % 9000) + 1000, 0);

            // Retorna o valor como string de 4 dígitos.
            return factor.ToString("D4");
        }

        /// <summary>
        /// Calcula o DAC (Dígito de Autoconferência) usando o módulo 11.
        /// </summary>
        /// <param name="partialCode">Código parcial do boleto (sem o DAC).</param>
        /// <returns>O dígito DAC calculado.</returns>
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
