using BoletoForBanrisul.Client.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client
{
    public class ClientSettings
    {
        public string RegisterBoletoUrl { get; set; } = "";
        public string GetBoletoUrl { get; set; } = "";
        public string ChangeDueDateUrl { get; set; } = "";
        public string GrantAbatementUrl { get; set; } = "";
        public string CancelBoletoUrl { get; set; } = "";
        public string IssueBoletoUrl { get; set; } = "";
        public string TokenUrl { get; set; } = "";

        public string BeneficiaryCode { get; set; } = "";

        public bool EnableLogging { get; set; } = false;
        public string LogDirectory { get; set; } = "";

        public RequestModeType RequestMode { get; set; } = RequestModeType.Production;
    }

    public enum RequestModeType
    {
        Production,
        Test
    }
}
