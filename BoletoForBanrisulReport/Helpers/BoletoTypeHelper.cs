using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report.Helpers
{
    public static class BoletoTypeHelper
    {
        public static string GetAbbreviation(Especie type)
        {
            return type switch
            {
                Especie.DuplicataMercantil => "DM",
                Especie.DuplicataServico => "DS",
                Especie.CartaoCredito => "CC",
                Especie.BoletoProposta => "BDP",
                Especie.Outros => "Outros",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
