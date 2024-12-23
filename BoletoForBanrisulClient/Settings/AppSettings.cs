using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.Settings
{
    internal class AppSettingsContainer
    {
        public AppSettings Production { get; set; } = new AppSettings();
        public AppSettings Sandbox { get; set; } = new AppSettings();
    }

    public class AppSettings
    {
        public ApiEnvironmentType Environment { get; set; } = ApiEnvironmentType.Production;

        public string BaseUrl { get; set; } = "";
        public string RegisterBoletoEndpoint { get; set; } = "";
        public string GetBoletoEndpoint { get; set; } = "";
        public string ChangeDueDateEndpoint { get; set; } = "";
        public string GrantAbatementEndpoint { get; set; } = "";
        public string CancelBoletoEndpoint { get; set; } = "";
        public string IssueBoletoEndpoint { get; set; } = "";
        public string TokenEndpoint { get; set; } = "";
    }

    public enum ApiEnvironmentType
    {
        Production,
        Sandbox
    }
}
