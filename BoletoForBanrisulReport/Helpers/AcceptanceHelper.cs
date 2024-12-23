using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report.Helpers
{
    public static class AcceptanceHelper
    {
        public static string GetAbbreviation(Aceite acceptance)
        {
            return acceptance switch
            {
                Aceite.Sim => "A",
                Aceite.Nao => "N",
                _ => throw new ArgumentOutOfRangeException(nameof(acceptance), acceptance, null)
            };
        }
    }
}
