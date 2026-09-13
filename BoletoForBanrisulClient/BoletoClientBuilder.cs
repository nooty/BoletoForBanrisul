using BoletoForBanrisul.Client.Auth;
using BoletoForBanrisul.Client.HttpClients;
using BoletoForBanrisul.Client.Settings;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client
{
    /// <summary>
    /// Builder for IBoletoClient.
    /// </summary>
    public class BoletoClientBuilder
    {
        private readonly BanrisulEndpointsLoader _settingsLoader;
        private IAuthenticator? _authenticator;
        private ClientSettings? _clientSettings;
        private bool _useSandbox = false;
        private ITokenStorage? _tokenStorage;
        private RequestModeType _requestMode = RequestModeType.Production;
        private HttpMessageHandler? _transportHandler;

        private string _beneficiaryCode = "";
        private string _clientId = "";
        private string _clientSecret = "";
        private string _scope = "boletos";

        private bool _enableLogging = false;
        private string? _logDirectory;

        public BoletoClientBuilder()
        {
            _settingsLoader = new BanrisulEndpointsLoader();
        }

        /// <summary>
        /// Replaces the transport handler at the bottom of the chain, so that a test can exercise the real
        /// pipeline - Polly policies and <see cref="HttpClients.AuthenticationHandler"/> included - without
        /// a real socket.
        /// </summary>
        /// <remarks>
        /// It replaces the bottom of the chain, not the whole <see cref="HttpClient"/>, so that every layer
        /// above - retries, timeout, token attachment, the <c>bergs-beneficiario</c> header - stays active
        /// and under test. The built client never disposes it, same as the shared default transport.
        /// </remarks>
        /// <param name="handler">The transport handler to use in place of the default.</param>
        /// <returns></returns>
        internal BoletoClientBuilder SetTransportHandler(HttpMessageHandler handler)
        {
            _transportHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        /// <summary>
        /// Sets the token storage used for authentication.
        /// Optional: use it to supply a custom ITokenStorage implementation.
        /// </summary>
        /// <param name="tokenStorage">An ITokenStorage implementation for storing authentication tokens.</param>
        /// <returns></returns>
        public BoletoClientBuilder SetTokenStorage(ITokenStorage tokenStorage)
        {
            _tokenStorage = tokenStorage;
            return this;
        }

        /// <summary>
        /// Sets the credentials used to call the billing API.
        /// The credentials are required to configure the client.
        /// </summary>
        /// <param name="clientId">The Client ID issued by Banrisul.</param>
        /// <param name="clientSecret">The Client Secret issued by Banrisul.</param>
        /// <param name="scope">The authentication scope. Defaults to "boletos".</param>
        /// <returns></returns>
        public BoletoClientBuilder SetAuthentication(string clientId, string clientSecret, string scope = "boletos")
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _scope = scope;

            return this;
        }

        /// <summary>
        /// Sets the beneficiário code.
        /// The beneficiário code is required to configure the client.
        /// </summary>
        /// <param name="code">The beneficiário's full code.</param>
        /// <returns></returns>
        public BoletoClientBuilder SetBeneficiary(string code)
        {
            _beneficiaryCode = code;
            return this;
        }

        /// <summary>
        /// Points the client at the Sandbox environment, for testing.
        /// </summary>
        /// <returns></returns>
        public BoletoClientBuilder UseSandbox()
        {
            _useSandbox = true;
            return this;
        }

        /// <summary>
        /// Sets the request mode for the API endpoints.
        /// Use it to state whether requests are made in production or in test mode.
        /// Optional; when omitted, the default mode is 'Production'.
        /// </summary>
        /// <param name="requestMode">The request mode, either 'Production' or 'Test'.</param>
        /// <returns></returns>
        public BoletoClientBuilder SetRequestMode(RequestModeType requestMode)
        {
            _requestMode = requestMode;
            return this;
        }

        /// <summary>
        /// Enables logging of requests and responses.
        /// </summary>
        /// <param name="logDirectory">Directory the logs are written to. When empty, defaults to "Directory.GetCurrentDirectory()/Logs".</param>
        /// <returns></returns>
        public BoletoClientBuilder EnableLogging(string? logDirectory = null)
        {
            _enableLogging = true;
            _logDirectory = !string.IsNullOrEmpty(logDirectory)
                ? logDirectory
                : Path.Combine(Directory.GetCurrentDirectory(), "Logs");

            return this;
        }

        /// <summary>
        /// Creates a client instance with the configured settings.
        /// </summary>
        /// <returns></returns>
        public IBoletoClient Build()
        {
            _clientSettings = LoadClientSettings(_useSandbox ? ApiHost.Sandbox : ApiHost.Production);

            var authSettings = LoadAuthenticatorSettings(_clientSettings);

            _tokenStorage ??= new SharedInMemoryTokenStorage();

            // The authenticator gets its own HttpClient, deliberately separate from the one used below for
            // the API calls: that one carries the AuthenticationHandler, which in turn calls back into this
            // authenticator to attach the token on every request - reusing it here would be circular.
            // Both sit on the same shared transport, so the split costs no extra connections.
            var authenticatorHttpClient = new ResilientHttpClientBuilder().SetHandler(CreateTransportHandler()).Build();
            _authenticator = new Authenticator(authSettings, _tokenStorage, authenticatorHttpClient);

            var apiHttpClient = new ResilientHttpClientBuilder()
                .SetHandler(new AuthenticationHandler(_authenticator, _beneficiaryCode, CreateTransportHandler()))
                .Build();

            // Creates and returns the BoletoClient with the configured settings.
            return new BoletoClient(_clientSettings, _authenticator, apiHttpClient, authenticatorHttpClient);
        }

        private AuthenticatorSettings LoadAuthenticatorSettings(ClientSettings clientSettings)
        {
            if (string.IsNullOrEmpty(_clientId) ||
                string.IsNullOrEmpty(_clientSecret) ||
                string.IsNullOrEmpty(_scope))
                throw new InvalidOperationException("Erro no builder: 'ClientId', 'ClientSecret' ou 'Scope' não foram informados. Estes valores são necessários para a criação do client. Verifique as configurações de autenticação no builder.");

            var authSettings = new AuthenticatorSettings();

            authSettings.TokenUrl = clientSettings.TokenUrl;
            authSettings.ClientId = _clientId;
            authSettings.ClientSecret = _clientSecret;
            authSettings.Scope = _scope;

            authSettings.EnableLogging = clientSettings.EnableLogging;
            authSettings.LogDirectory = clientSettings.LogDirectory;

            return authSettings;
        }

        private ClientSettings LoadClientSettings(ApiHost host)
        {
            var endpoints = _settingsLoader.LoadSettings(host);

            if (string.IsNullOrEmpty(_beneficiaryCode))
                throw new InvalidOperationException("Erro no builder: O 'Código do Beneficiário' não foi informado. Este valor é obrigatório para a configuração do client. Verifique as configurações no builder e informe o código do beneficiário.");

            if(_enableLogging && string.IsNullOrEmpty(_logDirectory))
                throw new InvalidOperationException($"Erro no builder: O diretório para salvar os logs deve ser informado.");

            return new ClientSettings
            {
                RegisterBoletoUrl = CombineUrl(endpoints.BaseUrl, endpoints.RegisterBoletoEndpoint),
                GetBoletoUrl = CombineUrl(endpoints.BaseUrl, endpoints.GetBoletoEndpoint),
                ChangeDueDateUrl = CombineUrl(endpoints.BaseUrl, endpoints.ChangeDueDateEndpoint),
                GrantAbatementUrl = CombineUrl(endpoints.BaseUrl, endpoints.GrantAbatementEndpoint),
                CancelBoletoUrl = CombineUrl(endpoints.BaseUrl, endpoints.CancelBoletoEndpoint),
                IssueBoletoUrl = CombineUrl(endpoints.BaseUrl, endpoints.IssueBoletoEndpoint),
                TokenUrl = CombineUrl(endpoints.BaseUrl, endpoints.TokenEndpoint),
                EnableLogging = _enableLogging,
                LogDirectory = _logDirectory ?? "",
                RequestMode = _requestMode
            };
        }

        // The test override when set, otherwise the process-wide transport. One instance serves both
        // HttpClients safely because neither of them owns (or disposes) its handler chain.
        private HttpMessageHandler CreateTransportHandler()
        {
            return _transportHandler ?? DefaultTransportHandler.Instance;
        }

        private string CombineUrl(string baseUrl, string resource)
        {
            var baseUri = new Uri(baseUrl);
            var fullUri = new Uri(baseUri, resource);
            return fullUri.ToString();
        }
    }

}
