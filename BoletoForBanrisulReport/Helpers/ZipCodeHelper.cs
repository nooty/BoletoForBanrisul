using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report.Helpers
{
    public class ZipCodeHelper
    {
        public static string Format(string zipCode)
        {
            if (string.IsNullOrWhiteSpace(zipCode))
            {
                return string.Empty;
            }

            if(zipCode.Length != 8)
            {
                return zipCode;
            }

            // Formatar CEP no estilo XXXXX-XXX.
            return $"{zipCode.Substring(0, 5)}-{zipCode.Substring(5, 3)}";
        }
    }
}
