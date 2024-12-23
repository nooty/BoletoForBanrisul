using BoletoForBanrisul.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Report.ViewModels
{
    public class PrintViewModel
    {
        public string LogoPath { get; set; } = "";
        public string Barcode { get; set; } = "";
        public string DigitableLine { get; set; } = "";
        public string QRCode { get; set; } = "";
        public string PaymentLocation { get; set; } = "";
        public BeneficiarySettings Beneficiary { get; set; } = new BeneficiarySettings();
        public Boleto Boleto { get; set; } = new Boleto();
    }
}
