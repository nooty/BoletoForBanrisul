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

            // Format CPF (11 digits).
            if (cpfCnpj.Length == 11)
            {
                return $"{cpfCnpj.Substring(0, 3)}.{cpfCnpj.Substring(3, 3)}.{cpfCnpj.Substring(6, 3)}-{cpfCnpj.Substring(9, 2)}";
            }

            // Format CNPJ (14 characters; its 12-character base may be alphanumeric).
            if (cpfCnpj.Length == 14)
            {
                return $"{cpfCnpj.Substring(0, 2)}.{cpfCnpj.Substring(2, 3)}.{cpfCnpj.Substring(5, 3)}/{cpfCnpj.Substring(8, 4)}-{cpfCnpj.Substring(12, 2)}";
            }

            return cpfCnpj; // Preserve values whose length does not match CPF or CNPJ.
        }
    }
}
