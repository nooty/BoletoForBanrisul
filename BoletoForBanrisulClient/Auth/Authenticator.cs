using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Client.Exceptions.Auth;
using BoletoForBanrisul.Client.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Security.Cryptography;
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
            _tokenKey = ComputeTokenKey(_settings.ClientId, _settings.ClientSecret);
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            return await _tokenStorage.GetOrAddToken(_tokenKey, RequestNewTokenAsync, cancellationToken);
        }

        // Gera uma chave de cache que não expõe o client secret para implementações de ITokenStorage.
        private static string ComputeTokenKey(string clientId, string clientSecret)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private async Task<OAuthToken> RequestNewTokenAsync(CancellationToken cancellationToken)
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

            var response = await _httpClient.SendAsync(request, cancellationToken);

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
                var redactedRequestContent = SensitiveDataRedactor.RedactFormUrlEncodedField(requestContent, "client_secret");
                var requestLog = new
                {
                    Method = request.Method.ToString(),
                    Url = request.RequestUri.ToString(),
                    Headers = request.Headers.ToDictionary(h => h.Key, h => h.Value),
                    Content = redactedRequestContent
                };

                var responseContent = response.Content != null ? await response.Content.ReadAsStringAsync() : string.Empty;
                var redactedResponseContent = SensitiveDataRedactor.RedactJsonField(responseContent, "access_token");
                var responseLog = new
                {
                    StatusCode = response.StatusCode,
                    Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value),
                    Content = redactedResponseContent
                };

                File.WriteAllText(requestFileName, JsonSerializer.Serialize(requestLog, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(requestContentFileName, redactedRequestContent);
                File.WriteAllText(responseFileName, JsonSerializer.Serialize(responseLog, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(responseContentFileName, redactedResponseContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging request/response: {ex.Message}");
            }
        }

    }
}
