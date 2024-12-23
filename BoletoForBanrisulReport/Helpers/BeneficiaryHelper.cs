using System;
using System.Collections.Generic;
using System.Text;

namespace BoletoForBanrisul.Report.Helpers
{
    public class BeneficiaryHelper
    {
        public static string Format(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            if (code.Length != 13)
            {
                return code;
            }

            return $"{code.Substring(0, 4)} / {code.Substring(4, 9)}";
        }
    }
}
