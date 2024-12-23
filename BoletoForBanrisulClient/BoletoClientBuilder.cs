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
    /// Builder para IBoletoClient.
    /// </summary>
    public class BoletoClientBuilder
    {
        private readonly AppSettingsLoader _settingsLoader;
        private HttpClient? _httpClient;
        private HttpClient? _customHttpClient;
        private IAuthenticator? _authenticator;
        private ClientSettings? _clientSettings;
        private bool _useSandbox = false;
        private ITokenStorage? _tokenStorage;
        private RequestModeType _requestMode = RequestModeType.Production;

        private string _beneficiaryCode = "";
        private string _clientId = "";
        private string _clientSecret = "";
        private string _scope = "boletos";

        private bool _enableLogging = false;
        private string? _logDirectory;

        public BoletoClientBuilder()
        {
            _settingsLoader = new AppSettingsLoader();
        }

        /// <summary>
        /// Define o cliente HTTP a ser utilizado.
        /// Esta configuração é opcional e pode ser utilizada para definir uma instância personalizada de HttpClient.
        /// </summary>
        /// <param name="httpClient">Uma instância do HttpClient.</param>
        /// <returns></returns>
        public BoletoClientBuilder SetHttpClient(HttpClient httpClient)
        {
            _customHttpClient = httpClient;
            return this;
        }

        /// <summary>
        /// Define o armazenamento de tokens para autenticação.
        /// Esta configuração é opcional e pode ser utilizado para definir uma implementação personalizada de ITokenStorage.
        /// </summary>
        /// <param name="tokenStorage">Uma implementação de ITokenStorage para armazenar tokens de autenticação.</param>
        /// <returns></returns>
        public BoletoClientBuilder SetTokenStorage(ITokenStorage tokenStorage)
        {
            _tokenStorage = tokenStorage;
            return this;
        }

        /// <summary>
        /// Define as credenciais de autenticação para utilizar a API de cobrança.
        /// As credenciais de autenticação são obrigatórias para configurar o client.
        /// </summary>
        /// <param name="clientId">O Client ID fornecido pelo Banrisul.</param>
        /// <param name="clientSecret">O Client Secret fornecido pelo Banrisul.</param>
        /// <param name="scope">O escopo para autenticação. O padrão é "boletos".</param>
        /// <returns></returns>
        public BoletoClientBuilder SetAuthentication(string clientId, string clientSecret, string scope = "boletos")
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _scope = scope;

            return this;
        }

        /// <summary>
        /// Seta o código do beneficiário.
        /// O código do beneficiário é obrigatório para configurar o client.
        /// </summary>
        /// <param name="code">Código completo do beneficiário.</param>
        /// <returns></returns>
        public BoletoClientBuilder SetBeneficiary(string code)
        {
            _beneficiaryCode = code;
            return this;
        }

        /// <summary>
        /// Indica que o client deve usar o ambiente de Sandbox para testes.
        /// </summary>
        /// <returns></returns>
        public BoletoClientBuilder UseSandbox()
        {
            _useSandbox = true;
            return this;
        }

        /// <summary>
        /// Define o modo da requisição para os endpoints da API.
        /// Use este método para especificar se as requisições devem ser feitas no modo de produção ou de teste.
        /// Esta configuração é opcional; se não for informada, o modo padrão é 'Production'.
        /// </summary>
        /// <param name="requestMode">O modo da requisição, podendo ser 'Production' ou 'Test'.</param>
        /// <returns></returns>
        public BoletoClientBuilder SetRequestMode(RequestModeType requestMode)
        {
            _requestMode = requestMode;
            return this;
        }

        /// <summary>
        /// Habilita o registro de logs das requisições e respostas.
        /// </summary>
        /// <param name="logDirectory">Diretório onde os logs devem ser salvos. Se não preenchido, usa o diretório padrão "Directory.GetCurrentDirectory()/Logs".</param>
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
        /// Cria uma instância do client com as configurações aplicadas.
        /// </summary>
        /// <returns></returns>
        public IBoletoClient Build()
        {
            _clientSettings = LoadClientSettings(_useSandbox ? ApiEnvironmentType.Sandbox : ApiEnvironmentType.Production);

            var authSettings = LoadAuthenticatorSettings(_clientSettings);

            var httpClient = _customHttpClient ?? GetDefaultHttpClient();

            _tokenStorage ??= new InMemoryTokenStorage();

            _authenticator = new Authenticator(authSettings, _tokenStorage, httpClient);

            // Cria e retorna o BoletoClient com as configurações aplicadas.
            return new BoletoClient(_clientSettings, _authenticator, httpClient);
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

        private ClientSettings LoadClientSettings(ApiEnvironmentType environmentType)
        {
            var environmentSettings = _settingsLoader.LoadSettings(environmentType);

            if (string.IsNullOrEmpty(_beneficiaryCode))
                throw new InvalidOperationException("Erro no builder: O 'Código do Beneficiário' não foi informado. Este valor é obrigatório para a configuração do client. Verifique as configurações no builder e informe o código do beneficiário.");

            if(_enableLogging && string.IsNullOrEmpty(_logDirectory))
                throw new InvalidOperationException($"Erro no builder: O diretório para salvar os logs deve ser informado.");

            return new ClientSettings
            {
                RegisterBoletoUrl = CombineUrl(environmentSettings.BaseUrl, environmentSettings.RegisterBoletoEndpoint),
                GetBoletoUrl = CombineUrl(environmentSettings.BaseUrl, environmentSettings.GetBoletoEndpoint),
                ChangeDueDateUrl = CombineUrl(environmentSettings.BaseUrl, environmentSettings.ChangeDueDateEndpoint),
                GrantAbatementUrl = CombineUrl(environmentSettings.BaseUrl, environmentSettings.GrantAbatementEndpoint),
                CancelBoletoUrl = CombineUrl(environmentSettings.BaseUrl, environmentSettings.CancelBoletoEndpoint),
                IssueBoletoUrl = CombineUrl(environmentSettings.BaseUrl, environmentSettings.IssueBoletoEndpoint),
                TokenUrl = CombineUrl(environmentSettings.BaseUrl, environmentSettings.TokenEndpoint),
                BeneficiaryCode = _beneficiaryCode,
                EnableLogging = _enableLogging,
                LogDirectory = _logDirectory ?? "",
                RequestMode = _requestMode
            };
        }

        private HttpClient GetDefaultHttpClient()
        {
            if (_httpClient != null)
                return _httpClient;

            _httpClient = new ResilientHttpClientBuilder().Build();

            return _httpClient;
        }

        private string CombineUrl(string baseUrl, string resource)
        {
            var baseUri = new Uri(baseUrl);
            var fullUri = new Uri(baseUri, resource);
            return fullUri.ToString();
        }
    }

}
