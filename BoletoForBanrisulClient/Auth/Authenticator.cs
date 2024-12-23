using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Client.Exceptions.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.Auth
{
    public class Authenticator : IAuthenticator
    {
        private readonly ITokenStorage _tokenStorage;
        private readonly HttpClient _httpClient;
        private readonly AuthenticatorSettings _settings;
        private readonly string _tokenKey;

        public Authenticator(AuthenticatorSettings settings,
            ITokenStorage tokenStorage,
            HttpClient httpClient)
        {
            _tokenStorage = tokenStorage;
            _httpClient = httpClient;
            _settings = settings;
            _tokenKey = $"{_settings.ClientId}:{_settings.ClientSecret}";
        }

        public async Task<string> GetAccessTokenAsync()
        {
            return await _tokenStorage.GetOrAddToken(_tokenKey, RequestNewTokenAsync);
        }

        private async Task<OAuthToken> RequestNewTokenAsync()
        {
            var formData = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "scope", _settings.Scope },
                { "client_id", _settings.ClientId },
                { "client_secret", _settings.ClientSecret }
            };

            var content = new FormUrlEncodedContent(formData);

            var request = new HttpRequestMessage(HttpMethod.Post, _settings.TokenUrl)
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request);

            await LogRequestResponseAsync(request, response);

            // Lança exceção se o código de status for 401 (Unauthorized).
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedException();
            }

            // Lança exceção genérica para qualquer outro código de erro.
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                throw new AuthenticationException($"ERRO NA AUTENTICAÇÃO. {response.StatusCode}. Detalhes: {errorMessage}");
            }

            var tokenResponse = await response.Content.ReadAsStringAsync();

            // Desserializa o token OAuth.
            var token = JsonSerializer.Deserialize<OAuthToken>(tokenResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = new SnakeCaseLowerNamingPolicy()
            });

            if (token == null)
            {
                throw new TokenDeserializationException();
            }

            token.ExpirationTime = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);

            return token;
        }

        private async Task LogRequestResponseAsync(HttpRequestMessage request, HttpResponseMessage response)
        {
            if (!_settings.EnableLogging || string.IsNullOrEmpty(_settings.LogDirectory))
                return;

            try
            {
                Directory.CreateDirectory(_settings.LogDirectory);

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                var guid = Guid.NewGuid().ToString();
                var requestFileName = Path.Combine(_settings.LogDirectory, $"AuthRequest_{timestamp}_{guid}.json");
                var requestContentFileName = Path.Combine(_settings.LogDirectory, $"AuthRequest_Content_{timestamp}_{guid}.json");
                var responseFileName = Path.Combine(_settings.LogDirectory, $"AuthResponse_{timestamp}_{guid}.json");
                var responseContentFileName = Path.Combine(_settings.LogDirectory, $"AuthResponse_Content_{timestamp}_{guid}.json");

                var requestContent = request.Content != null ? await request.Content.ReadAsStringAsync() : string.Empty;
                var requestLog = new
                {
                    Method = request.Method.ToString(),
                    Url = request.RequestUri.ToString(),
                    Headers = request.Headers.ToDictionary(h => h.Key, h => h.Value),
                    Content = requestContent
                };

                var responseContent = response.Content != null ? await response.Content.ReadAsStringAsync() : string.Empty;
                var responseLog = new
                {
                    StatusCode = response.StatusCode,
                    Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value),
                    Content = responseContent
                };

                File.WriteAllText(requestFileName, JsonSerializer.Serialize(requestLog, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(requestContentFileName, requestContent);
                File.WriteAllText(responseFileName, JsonSerializer.Serialize(responseLog, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(responseContentFileName, responseContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging request/response: {ex.Message}");
            }
        }

    }
}
