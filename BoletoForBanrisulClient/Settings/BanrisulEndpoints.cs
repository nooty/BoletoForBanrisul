using System.Text.Json.Serialization;

namespace BoletoForBanrisul.Client.Settings
{
    /// <summary>Wrapper matching the shape of the embedded <c>Resources/UrlSettings.json</c> resource.</summary>
    internal class BanrisulEndpointsContainer
    {
        /// <summary>Hostname and paths for the production host.</summary>
        [JsonPropertyName("Production")]
        public BanrisulEndpoints Production { get; set; } = new BanrisulEndpoints();

        /// <summary>Hostname and paths for the sandbox host.</summary>
        [JsonPropertyName("Sandbox")]
        public BanrisulEndpoints Sandbox { get; set; } = new BanrisulEndpoints();
    }

    /// <summary>The Banrisul hostname and endpoint paths for one host, loaded from the embedded <c>UrlSettings.json</c>.</summary>
    /// <remarks>
    /// Every property that comes from the JSON carries an explicit <see cref="JsonPropertyNameAttribute"/>.
    /// Without one, the key is the property name, so renaming a property silently stops matching the resource
    /// and every URL is built against an empty endpoint - a 200-less failure that only shows up at runtime.
    /// <see cref="Host"/> has none because it is not in the resource: the loader sets it.
    /// </remarks>
    public class BanrisulEndpoints
    {
        /// <summary>Which of the two hosts these endpoints describe. Set by the loader, not read from the resource.</summary>
        [JsonIgnore]
        public ApiHost Host { get; set; } = ApiHost.Production;

        /// <summary>The base URL for this host (for example <c>https://api.banrisul.com.br</c>).</summary>
        [JsonPropertyName("BaseUrl")]
        public string BaseUrl { get; set; } = "";

        /// <summary>Path of the endpoint used to register a new bank slip.</summary>
        [JsonPropertyName("RegisterBoletoEndpoint")]
        public string RegisterBoletoEndpoint { get; set; } = "";

        /// <summary>Path of the endpoint used to query a bank slip.</summary>
        [JsonPropertyName("GetBoletoEndpoint")]
        public string GetBoletoEndpoint { get; set; } = "";

        /// <summary>Path of the endpoint used to change a bank slip's due date.</summary>
        [JsonPropertyName("ChangeDueDateEndpoint")]
        public string ChangeDueDateEndpoint { get; set; } = "";

        /// <summary>Path of the endpoint used to grant an abatement on a bank slip.</summary>
        [JsonPropertyName("GrantAbatementEndpoint")]
        public string GrantAbatementEndpoint { get; set; } = "";

        /// <summary>Path of the endpoint used to cancel (write off) a bank slip.</summary>
        [JsonPropertyName("CancelBoletoEndpoint")]
        public string CancelBoletoEndpoint { get; set; } = "";

        /// <summary>Path of the endpoint used to issue a bank slip in PDF format.</summary>
        [JsonPropertyName("IssueBoletoEndpoint")]
        public string IssueBoletoEndpoint { get; set; } = "";

        /// <summary>Path of the OAuth2 token endpoint.</summary>
        [JsonPropertyName("TokenEndpoint")]
        public string TokenEndpoint { get; set; } = "";
    }

    /// <summary>
    /// Which Banrisul host a client sends to.
    /// </summary>
    /// <remarks>
    /// <b>Not to be confused with <see cref="Core.Models.Types.Ambiente"/></b>, which is a field on the
    /// bank slip itself (Producao/Teste) and decides whether the bank persists it. The two are independent
    /// concepts: this one picks the machine the request goes to, that one tells the bank what to do with the
    /// payload once it arrives.
    /// </remarks>
    public enum ApiHost
    {
        /// <summary>Production (<c>api.banrisul.com.br</c>).</summary>
        Production,

        /// <summary>Sandbox (<c>apidev.banrisul.com.br</c>).</summary>
        Sandbox
    }
}
