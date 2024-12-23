using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.Auth
{
    public class AuthenticatorSettings
    {
        public string TokenUrl { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string Scope { get; set; } = "boletos";

        public bool EnableLogging { get; set; } = false;
        public string LogDirectory { get; set; } = "";
    }
}
