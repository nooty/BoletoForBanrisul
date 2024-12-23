using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Models.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report.Helpers
{
    public class WalletTypeHelper
    {
        public static string GetAbbreviation(Carteira? code)
        {
            if (code == null)
            {
                return "";
            }

            return (((int)code) + "").PadLeft(2, '0');
        }
    }
}
