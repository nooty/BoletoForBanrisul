using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report.Helpers
{
    public class CnpjCpfHelper
    {
        public static string Format(string cpfCnpj)
        {
            if (string.IsNullOrWhiteSpace(cpfCnpj))
            {
                return string.Empty;
            }

            cpfCnpj = cpfCnpj.Trim();

            // Formatar CPF (11 dígitos)
            if (cpfCnpj.Length == 11)
            {
                return $"{cpfCnpj.Substring(0, 3)}.{cpfCnpj.Substring(3, 3)}.{cpfCnpj.Substring(6, 3)}-{cpfCnpj.Substring(9, 2)}";
            }

            // Formatar CNPJ (14 dígitos)
            if (cpfCnpj.Length == 14)
            {
                return $"{cpfCnpj.Substring(0, 2)}.{cpfCnpj.Substring(2, 3)}.{cpfCnpj.Substring(5, 3)}/{cpfCnpj.Substring(8, 4)}-{cpfCnpj.Substring(12, 2)}";
            }

            return cpfCnpj; // Se o tamanho não for compatível com CPF ou CNPJ
        }
    }
}
