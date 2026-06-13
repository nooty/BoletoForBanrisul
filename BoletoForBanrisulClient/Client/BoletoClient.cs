using BoletoForBanrisul.Client.Auth;
using BoletoForBanrisul.Client.Helpers;
using BoletoForBanrisul.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BoletoForBanrisul.Client.Models.Operations;
using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Client.Exceptions.Api;
using BoletoForBanrisul.Client.Models.Responses;

namespace BoletoForBanrisul.Client
{
    public class BoletoClient : IBoletoClient
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthenticator _authenticator;
        private readonly ClientSettings _settings;

        public IAuthenticator Authenticator => _authenticator;

        public BoletoClient(ClientSettings config, IAuthenticator authenticator, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _authenticator = authenticator;
            _settings = config;
        }

        public async Task<CreateBoletoResponse> CreateBoletoAsync(Boleto boleto)
        {
            var boletoJson = boleto.ToJson();
            var jsonContent = new StringContent(boletoJson, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, _settings.RegisterBoletoUrl)
            {
                Content = jsonContent
            };

            AddBeneficiaryHeader(request);

            var response = await HandleRequestAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();
            return Deserialize<CreateBoletoResponse>(responseContent);
        }

        public async Task<GetBoletoResponse> GetBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, GetUrlForIdType(_settings.GetBoletoUrl, idBoleto, typeId));

            AddBeneficiaryHeader(request);

            var response = await HandleRequestAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();
            return Deserialize<GetBoletoResponse>(responseContent);
        }

        public async Task<GrantAbatementResponse> GrantAbatementAsync(string idBoleto, decimal abatement, BoletoIdentifierType? typeId = null)
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

            AddBeneficiaryHeader(request);
            AddRequestModeHeader(request);

            var response = await HandleRequestAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();
            return Deserialize<GrantAbatementResponse>(responseContent);
        }

        public async Task<ChangeDueDateResponse> ChangeDueDateAsync(string idBoleto, DateTime newDueDate, BoletoIdentifierType? typeId = null)
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

            AddBeneficiaryHeader(request);
            AddRequestModeHeader(request);

            var response = await HandleRequestAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();
            return Deserialize<ChangeDueDateResponse>(responseContent);
        }

        public async Task<CancelBoletoResponse> CancelBoletoAsync(string idBoleto, BoletoIdentifierType? typeId = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, GetUrlForIdType(_settings.CancelBoletoUrl, idBoleto, typeId));

            AddBeneficiaryHeader(request);
            AddRequestModeHeader(request);

            var response = await HandleRequestAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();
            return Deserialize<CancelBoletoResponse>(responseContent);
        }

        public async Task<byte[]> IssueBoletoAsync(string idBoleto, BoletoIdentifierType? typeId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, GetUrlForIdType(_settings.IssueBoletoUrl, idBoleto, typeId));

            AddBeneficiaryHeader(request);
            AddRequestModeHeader(request);

            var response = await HandleRequestAsync(request);

            await HandleErrors(response);

            var responseContent = Deserialize<IssueBoletoResponse>(await response.Content.ReadAsStringAsync());

            return Convert.FromBase64String(responseContent.Titulo.Boleto);
        }

        private async Task<HttpResponseMessage> HandleRequestAsync(HttpRequestMessage request)
        {
            await AddAuthorizationHeaderAsync(request);

            var response = await _httpClient.SendAsync(request);
            
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

        // Método auxiliar para adicionar o header Authorization.
        private async Task AddAuthorizationHeaderAsync(HttpRequestMessage request)
        {
            string token = await _authenticator.GetAccessTokenAsync();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        // Método auxiliar para adicionar o header bergs-beneficiario.
        private void AddBeneficiaryHeader(HttpRequestMessage request)
        {
            request.Headers.Add("bergs-beneficiario", _settings.BeneficiaryCode);
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
