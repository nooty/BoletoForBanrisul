using BoletoForBanrisul.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report.Helpers
{
    public class CurrencyHelper
    {
        public static string Format(decimal value)
        {
            var cultureBR = new CultureInfo("pt-BR");
            return value.ToString("C", cultureBR);
        }
    }
}
