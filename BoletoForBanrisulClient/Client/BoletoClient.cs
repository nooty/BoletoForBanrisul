using BoletoForBanrisul.Client.Auth;
using BoletoForBanrisul.Client.Helpers;
using BoletoForBanrisul.Client.HttpClients;
using BoletoForBanrisul.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BoletoForBanrisul.Client.Models.Operations;
using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Validation;
using BoletoForBanrisul.Client.Exceptions.Api;
using BoletoForBanrisul.Client.Models.Responses;

namespace BoletoForBanrisul.Client
{
    public class BoletoClient : IBoletoClient
    {
        private readonly HttpClient _httpClient;
        private readonly HttpClient _authenticatorHttpClient;
        private readonly IAuthenticator _authenticator;
        private readonly ClientSettings _settings;
        private bool _disposed;

        public IAuthenticator Authenticator => _authenticator;

        /// <param name="config">Configurações do client.</param>
        /// <param name="authenticator">Autenticador utilizado para obter o token de acesso.</param>
        /// <param name="httpClient">HttpClient utilizado para as requisições à API, com o AuthenticationHandler já configurado.</param>
        /// <param name="authenticatorHttpClient">HttpClient utilizado pelo <paramref name="authenticator"/> para a troca do token OAuth.</param>
        /// <remarks>
        /// O <see cref="BoletoClientBuilder"/> é sempre o proprietário dos dois HttpClient - nada mais os
        /// referencia - então ambos são descartados em <see cref="Dispose"/> incondicionalmente.
        /// </remarks>
        public BoletoClient(ClientSettings config, IAuthenticator authenticator, HttpClient httpClient, HttpClient authenticatorHttpClient)
        {
            _httpClient = httpClient;
            _authenticatorHttpClient = authenticatorHttpClient;
            _authenticator = authenticator;
            _settings = config;
        }

        /// <summary>
        /// Libera os recursos utilizados pelo client, descartando os dois HttpClient criados pelo
        /// <see cref="BoletoClientBuilder"/> (o da API e o do autenticador).
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _httpClient.Dispose();
            _authenticatorHttpClient.Dispose();

            _disposed = true;
        }

        public async Task<CreateBoletoResponse> CreateBoletoAsync(Boleto boleto, CancellationToken cancellationToken = default)
        {
            RecursiveValidator.ValidateAndThrow(boleto);

            var boletoJson = boleto.ToJson();
            var jsonContent = new StringContent(boletoJson, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, _settings.RegisterBoletoUrl)
            {
                Content = jsonContent
            };

            // Deliberadamente sem MarkRetrySafe: esta é a única operação que cria um recurso novo a
            // cada envio. Se o banco registrar o boleto e a resposta se perder na volta, um reenvio
            // registraria um segundo - a API não documenta idempotência aqui. A falha transitória
            // sobe para quem chamou, que tem como reconciliar por SeuNumero antes de tentar de novo.
            var response = await HandleRequestAsync(request, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync();
            return Deserialize<CreateBoletoResponse>(responseContent);
        }

        public async Task<GetBoletoResponse> GetBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, GetUrlForIdType(_settings.GetBoletoUrl, idBoleto, typeId));

            var response = await HandleRequestAsync(request, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync();
            return Deserialize<GetBoletoResponse>(responseContent);
        }

        public async Task<GrantAbatementResponse> GrantAbatementAsync(string idBoleto, decimal abatement, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default)
        {
            var operation = new GrantAbatementRequest
            {
                Titulo = new GrantAbatementDetails
                {
                    Instrucoes = new GrantAbatementInstructions
                    {
                        Abatimento = new GrantAbatementValue
                        {
                            Valor = abatement
                        }
                    }
                }
            };

            var url = GetUrlForIdType(_settings.GrantAbatementUrl, idBoleto, typeId, "04");

            var jsonContent = new StringContent(operation.ToJson(), Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            {
                Content = jsonContent
            };

            AddRequestModeHeader(request);

            // Grava um valor absoluto de abatimento no boleto que já existe: reenviar chega ao mesmo
            // estado final, sem criar nada.
            request.MarkRetrySafe();

            var response = await HandleRequestAsync(request, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync();
            return Deserialize<GrantAbatementResponse>(responseContent);
        }

        public async Task<ChangeDueDateResponse> ChangeDueDateAsync(string idBoleto, DateTime newDueDate, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default)
        {
            var operation = new ChangeDueDateRequest
            {
                Titulo = new ChangeDueDateDetails
                {
                    DataVencimento = newDueDate
                }
            };

            var jsonContent = new StringContent(operation.ToJson(), Encoding.UTF8, "application/json");

            var url = GetUrlForIdType(_settings.ChangeDueDateUrl, idBoleto, typeId, "06");

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            {
                Content = jsonContent
            };

            AddRequestModeHeader(request);

            // Grava uma data de vencimento absoluta no boleto que já existe: reenviar chega ao mesmo
            // estado final, sem criar nada.
            request.MarkRetrySafe();

            var response = await HandleRequestAsync(request, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync();
            return Deserialize<ChangeDueDateResponse>(responseContent);
        }

        public async Task<CancelBoletoResponse> CancelBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, GetUrlForIdType(_settings.CancelBoletoUrl, idBoleto, typeId));

            AddRequestModeHeader(request);

            // POST, mas é uma transição de estado do boleto que já existe para "baixado": reenviar
            // chega ao mesmo estado final, sem criar nada.
            request.MarkRetrySafe();

            var response = await HandleRequestAsync(request, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync();
            return Deserialize<CancelBoletoResponse>(responseContent);
        }

        public async Task<byte[]> IssueBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, GetUrlForIdType(_settings.IssueBoletoUrl, idBoleto, typeId));

            AddRequestModeHeader(request);

            var response = await HandleRequestAsync(request, cancellationToken);

            var responseContent = Deserialize<IssueBoletoResponse>(await response.Content.ReadAsStringAsync());

            return Convert.FromBase64String(responseContent.Titulo.Boleto);
        }

        private async Task<HttpResponseMessage> HandleRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            await LogRequestResponseAsync(request, response);

            await HandleErrors(response);

            return response;
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
                var requestFileName = Path.Combine(_settings.LogDirectory, $"Request_{timestamp}_{guid}.json");
                var requestContentFileName = Path.Combine(_settings.LogDirectory, $"Request_Content_{timestamp}_{guid}.json");
                var responseFileName = Path.Combine(_settings.LogDirectory, $"Response_{timestamp}_{guid}.json");
                var responseContentFileName = Path.Combine(_settings.LogDirectory, $"Response_Content_{timestamp}_{guid}.json");

                var requestContent = request.Content != null ? await request.Content.ReadAsStringAsync() : string.Empty;
                var requestLog = new
                {
                    Method = request.Method.ToString(),
                    Url = request.RequestUri.ToString(),
                    Headers = SensitiveDataRedactor.RedactAuthorizationHeader(request.Headers.ToDictionary(h => h.Key, h => h.Value)),
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

        private void AddRequestModeHeader(HttpRequestMessage request)
        {
            var requestMode = _settings.RequestMode == RequestModeType.Production ? "P" : "T";
            request.Headers.Add("bergs-ambiente", requestMode);
        }


        private string GetUrlForIdType(string endpoint, string id, BoletoIdentifierType? typeId, string operationId = "")
        {
            var transformedEndpoint = endpoint.Replace("{idBoleto}", Uri.EscapeDataString(id));

            var queryParams = new List<string>();

            if (typeId.HasValue)
            {
                queryParams.Add(typeId.Value switch
                {
                    BoletoIdentifierType.Barcode => "tipo_id=CODIGO_BARRAS",
                    BoletoIdentifierType.DigitableLine => "tipo_id=LINHA_DIGITAVEL",
                    _ => "tipo_id=NOSSO_NUMERO"
                });
            }

            if (!string.IsNullOrEmpty(operationId))
            {
                queryParams.Add($"tipo_alteracao={operationId}");
            }

            var queryString = queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : string.Empty;

            return $"{transformedEndpoint}{queryString}";
        }

        private async Task HandleErrors(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();

                var errorResponse = Deserialize<ErrorResponse>(content);

                var ocurrences = errorResponse?.Ocorrencias;

                ApiException exception;

                HttpErrorCode errorCode = Enum.IsDefined(typeof(HttpErrorCode), (int)response.StatusCode) ? 
                    (HttpErrorCode)response.StatusCode : HttpErrorCode.Unknown;

                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.BadRequest:
                        exception = new InvalidRequestException();
                        break;
                    case System.Net.HttpStatusCode.Unauthorized:
                        throw new Exceptions.Auth.UnauthorizedException();
                    case System.Net.HttpStatusCode.NotFound:
                        exception = new NotFoundException();
                        break;
                    case (System.Net.HttpStatusCode)422:
                        exception = new UnprocessableEntityException();
                        break;
                    case System.Net.HttpStatusCode.InternalServerError:
                        exception = new InternalServerErrorException();
                        break;
                    case System.Net.HttpStatusCode.ServiceUnavailable:
                        exception = new ServiceUnavailableException();
                        break;
                    case (System.Net.HttpStatusCode)429:
                        exception = new TooManyRequestsException();
                        break;
                    default:
                        exception = new ApiException($"Erro inesperado durante a comunicação com a API. Detalhes: {content}.", errorCode);
                        break;
                }

                if(ocurrences != null && ocurrences.Count > 0)
                    exception.Ocurrences.AddRange(ocurrences.Select(o => new ApiOcurrence(o.Codigo, o.Mensagem, o.Complemento)).ToList());

                throw exception;
            }
        }

        private T Deserialize<T>(string content)
        {
            var contentDeserialized = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = new SnakeCaseLowerNamingPolicy()
            });

            if (contentDeserialized == null)
                throw new ResponseDeserializationException(content);

            return contentDeserialized;
        }
    }
}
