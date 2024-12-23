using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.Auth
{
    public class OAuthToken
    {
        public string AccessToken { get; set; } = "";
        public int ExpiresIn { get; set; } // Em segundos
        public DateTimeOffset ExpirationTime { get; set; }
    }
}
