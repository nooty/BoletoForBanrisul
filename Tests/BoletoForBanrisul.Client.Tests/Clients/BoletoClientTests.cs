using BoletoForBanrisul.Client.Exceptions.Api;
using BoletoForBanrisul.Client.Tests.TestSupport;
using BoletoForBanrisul.Core.Validation;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace BoletoForBanrisul.Client.Tests.Clients
{
    [Trait("Category", "Unit")]
    public class BoletoClientTests
    {
        private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        /// <summary>A minimal <c>CreateBoletoResponse</c>/<c>GetBoletoResponse</c> body: only the fields these tests read.</summary>
        private const string MinimalTituloResponseJson =
            "{\"retorno\":\"01\",\"titulo\":{\"seu_numero\":\"64452-1\",\"nosso_numero\":\"0000038806\"}}";

        /// <summary>A minimal body for the four operation endpoints (Change/Grant/Cancel/Issue), which all share the same <c>{"titulo": {...}}</c> envelope shape.</summary>
        private const string MinimalOperationResponseJson =
            "{\"retorno\":\"01\",\"titulo\":{\"nosso_numero\":\"0000038806\"}}";

        private static string BodyOf(StubHttpMessageHandler stub, HttpRequestMessage request) =>
            stub.RequestBodies[stub.Requests.IndexOf(request)]!;

        // --- CreateBoletoAsync: validates locally, then POSTs. ---

        [Fact]
        public async Task CreateBoletoAsync_IssuesPost_ToRegisterBoletoUrl_WithSerializedBody()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(
                _ => JsonResponse(HttpStatusCode.Created, MinimalTituloResponseJson),
                beneficiaryCode: "0010000001088");

            using var client = builder.Build();
            var boleto = BoletoFactory.CreateValid();

            var created = await client.CreateBoletoAsync(boleto);

            Assert.NotNull(created.Titulo);
            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal(HttpMethod.Post, sent.Method);
            Assert.EndsWith("/cobranca/v1/boletos", sent.RequestUri!.AbsolutePath);

            using var body = JsonDocument.Parse(BodyOf(stub, sent));
            Assert.Equal("64452-1", body.RootElement.GetProperty("titulo").GetProperty("seu_numero").GetString());
        }

        [Fact]
        public async Task CreateBoletoAsync_SendsOnly_TheBeneficiaryHeader_NotTheRequestModeHeader()
        {
            // Deliberate asymmetry, pinned rather than "fixed": of the six BoletoClient methods, only
            // CreateBoletoAsync and GetBoletoAsync never call AddRequestModeHeader - GrantAbatementAsync,
            // ChangeDueDateAsync, CancelBoletoAsync and IssueBoletoAsync all do. See BoletoClient.cs; this
            // test exists so a future edit that changes the set is a deliberate decision, not an accident.
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(
                _ => JsonResponse(HttpStatusCode.Created, MinimalTituloResponseJson),
                beneficiaryCode: "0010000001088");

            using var client = builder.Build();
            await client.CreateBoletoAsync(BoletoFactory.CreateValid());

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal("0010000001088", Assert.Single(sent.Headers.GetValues("bergs-beneficiario")));
            Assert.False(sent.Headers.Contains("bergs-ambiente"));
        }

        [Fact]
        public async Task CreateBoletoAsync_Throws_AndSendsNoRequest_ForAnInvalidBoleto()
        {
            // The most important test in this file: it proves local validation is a real short-circuit, not
            // just an early check whose failure is swallowed - no HTTP call (not even the token exchange for
            // some client shapes) escapes an invalid Boleto.
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(
                _ => throw new InvalidOperationException("Should never be called."));

            using var client = builder.Build();
            var boleto = BoletoFactory.CreateValid();
            boleto.Titulo.ValorNominal = -1m; // invalid: must be greater than zero unless Especie is CartaoCredito.

            await Assert.ThrowsAsync<ModelValidationException>(() => client.CreateBoletoAsync(boleto));

            Assert.Empty(stub.Requests);
        }

        [Fact]
        public async Task CreateBoletoAsync_ReportsTheDottedMemberPath_OnValidationFailure()
        {
            var (builder, _) = StubClientFactory.CreateBoletoClientBuilder(
                _ => throw new InvalidOperationException("Should never be called."));

            using var client = builder.Build();
            var boleto = BoletoFactory.CreateValid();
            boleto.Titulo.Pagador.Cep = "abc"; // invalid: must be 8 digits.

            var exception = await Assert.ThrowsAsync<ModelValidationException>(() => client.CreateBoletoAsync(boleto));

            Assert.Contains(exception.Errors, e => e.MemberNames.Contains("Titulo.Pagador.Cep"));
        }

        // --- GetBoletoAsync: GET, no request-mode header, tipo_id query building, id escaping. ---

        [Theory]
        [InlineData(null, "")]
        [InlineData(BoletoIdentifierType.OurNumber, "tipo_id=NOSSO_NUMERO")]
        [InlineData(BoletoIdentifierType.Barcode, "tipo_id=CODIGO_BARRAS")]
        [InlineData(BoletoIdentifierType.DigitableLine, "tipo_id=LINHA_DIGITAVEL")]
        public async Task GetBoletoAsync_BuildsTheTipoIdQuery_ForTheGivenIdentifierType(BoletoIdentifierType? typeId, string expectedQuery)
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(_ => JsonResponse(HttpStatusCode.OK, MinimalTituloResponseJson));
            using var client = builder.Build();

            await client.GetBoletoAsync("0000038806", typeId);

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal(HttpMethod.Get, sent.Method);

            if (string.IsNullOrEmpty(expectedQuery))
                Assert.Equal("", sent.RequestUri!.Query);
            else
                Assert.Contains(expectedQuery, sent.RequestUri!.Query);
        }

        [Fact]
        public async Task GetBoletoAsync_UrlEscapesTheIdBoleto()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(_ => JsonResponse(HttpStatusCode.OK, MinimalTituloResponseJson));
            using var client = builder.Build();

            await client.GetBoletoAsync("AB CD/12");

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Contains(Uri.EscapeDataString("AB CD/12"), sent.RequestUri!.OriginalString);
        }

        [Fact]
        public async Task GetBoletoAsync_SendsOnly_TheBeneficiaryHeader_NotTheRequestModeHeader()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(
                _ => JsonResponse(HttpStatusCode.OK, MinimalTituloResponseJson),
                beneficiaryCode: "0010000001088");

            using var client = builder.Build();
            await client.GetBoletoAsync("0000038806");

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal("0010000001088", Assert.Single(sent.Headers.GetValues("bergs-beneficiario")));
            Assert.False(sent.Headers.Contains("bergs-ambiente"));
        }

        // --- ChangeDueDateAsync: PATCH, tipo_alteracao=06, request-mode header present. ---

        [Fact]
        public async Task ChangeDueDateAsync_IssuesPatch_WithTipoAlteracao06_AndSerializedDueDate()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(_ => JsonResponse(HttpStatusCode.OK, MinimalOperationResponseJson));
            using var client = builder.Build();

            await client.ChangeDueDateAsync("0000038806", new DateTime(2026, 9, 30), BoletoIdentifierType.Barcode);

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal(new HttpMethod("PATCH"), sent.Method);
            Assert.Contains("tipo_id=CODIGO_BARRAS", sent.RequestUri!.Query);
            Assert.Contains("tipo_alteracao=06", sent.RequestUri!.Query);

            using var body = JsonDocument.Parse(BodyOf(stub, sent));
            Assert.Equal("2026-09-30", body.RootElement.GetProperty("titulo").GetProperty("data_vencimento").GetString());

            Assert.NotEmpty(sent.Headers.GetValues("bergs-beneficiario"));
            Assert.Equal("P", Assert.Single(sent.Headers.GetValues("bergs-ambiente")));
        }

        // --- GrantAbatementAsync: PATCH, tipo_alteracao=04, decimal serialized as "F2". ---

        [Fact]
        public async Task GrantAbatementAsync_IssuesPatch_WithTipoAlteracao04_AndSerializedAbatementValue()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(_ => JsonResponse(HttpStatusCode.OK, MinimalOperationResponseJson));
            using var client = builder.Build();

            await client.GrantAbatementAsync("0000038806", 12.3m);

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal(new HttpMethod("PATCH"), sent.Method);
            Assert.Contains("tipo_alteracao=04", sent.RequestUri!.Query);
            Assert.DoesNotContain("tipo_id", sent.RequestUri!.Query);

            using var body = JsonDocument.Parse(BodyOf(stub, sent));
            Assert.Equal(
                "12.30",
                body.RootElement.GetProperty("titulo").GetProperty("instrucoes").GetProperty("abatimento").GetProperty("valor").GetString());

            Assert.Equal("P", Assert.Single(sent.Headers.GetValues("bergs-ambiente")));
        }

        // --- CancelBoletoAsync: POST, request-mode header present, no body. ---

        [Fact]
        public async Task CancelBoletoAsync_IssuesPost_ToTheAssembledUrl_WithBothHeaders()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(
                _ => JsonResponse(HttpStatusCode.OK, MinimalOperationResponseJson),
                beneficiaryCode: "0010000001088");

            using var client = builder.Build();
            await client.CancelBoletoAsync("0000038806");

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal(HttpMethod.Post, sent.Method);
            Assert.EndsWith("/0000038806/baixar", sent.RequestUri!.AbsolutePath);
            Assert.Equal("0010000001088", Assert.Single(sent.Headers.GetValues("bergs-beneficiario")));
            Assert.Equal("P", Assert.Single(sent.Headers.GetValues("bergs-ambiente")));
        }

        // --- IssueBoletoAsync: GET, request-mode header present, base64-decoded response. ---

        [Fact]
        public async Task IssueBoletoAsync_Base64DecodesTheResponse_IntoBytes()
        {
            var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
            var responseJson = $"{{\"retorno\":\"01\",\"titulo\":{{\"boleto\":\"{Convert.ToBase64String(pdfBytes)}\"}}}}";
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(_ => JsonResponse(HttpStatusCode.OK, responseJson));

            using var client = builder.Build();
            var result = await client.IssueBoletoAsync("0000038806");

            Assert.Equal(pdfBytes, result);

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal(HttpMethod.Get, sent.Method);
            Assert.EndsWith("/emitir", sent.RequestUri!.AbsolutePath);
            Assert.Equal("P", Assert.Single(sent.Headers.GetValues("bergs-ambiente")));
        }

        [Fact]
        public async Task CancelBoletoAsync_SendsTestRequestMode_WhenTheBuilderIsConfiguredForIt()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(_ => JsonResponse(HttpStatusCode.OK, MinimalOperationResponseJson));
            builder.SetRequestMode(RequestModeType.Test);

            using var client = builder.Build();
            await client.CancelBoletoAsync("0000038806");

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal("T", Assert.Single(sent.Headers.GetValues("bergs-ambiente")));
        }

        // --- HandleErrors: status -> exception mapping. ---

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, typeof(InvalidRequestException))]
        [InlineData(HttpStatusCode.NotFound, typeof(NotFoundException))]
        [InlineData((HttpStatusCode)422, typeof(UnprocessableEntityException))]
        [InlineData((HttpStatusCode)429, typeof(TooManyRequestsException))]
        [InlineData(HttpStatusCode.InternalServerError, typeof(InternalServerErrorException))]
        [InlineData(HttpStatusCode.ServiceUnavailable, typeof(ServiceUnavailableException))]
        public async Task GetBoletoAsync_MapsTheHttpStatus_ToTheExpectedApiException(HttpStatusCode status, Type expectedExceptionType)
        {
            var (builder, _) = StubClientFactory.CreateBoletoClientBuilder(
                _ => JsonResponse(status, "{\"ocorrencias\":[]}"));

            using var client = builder.Build();

            var exception = await Assert.ThrowsAnyAsync<ApiException>(() => client.GetBoletoAsync("0000038806"));

            Assert.IsType(expectedExceptionType, exception);
        }

        [Fact]
        public async Task GetBoletoAsync_UnmappedStatus_ThrowsAGenericApiException_CarryingTheStatusCode()
        {
            var (builder, _) = StubClientFactory.CreateBoletoClientBuilder(
                _ => JsonResponse(HttpStatusCode.Conflict, "{\"ocorrencias\":[]}"));

            using var client = builder.Build();

            var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetBoletoAsync("0000038806"));

            Assert.Equal(HttpErrorCode.Conflict, exception.HttpStatusCode);
        }

        [Fact]
        public async Task GetBoletoAsync_PopulatesOcurrences_FromTheErrorBody()
        {
            const string errorJson = "{\"ocorrencias\":[{\"codigo\":\"E01\",\"mensagem\":\"Campo inv\\u00e1lido\",\"complemento\":\"seu_numero\"}]}";
            var (builder, _) = StubClientFactory.CreateBoletoClientBuilder(_ => JsonResponse(HttpStatusCode.BadRequest, errorJson));

            using var client = builder.Build();
            var exception = await Assert.ThrowsAsync<InvalidRequestException>(() => client.GetBoletoAsync("0000038806"));

            var ocurrence = Assert.Single(exception.Ocurrences);
            Assert.Equal("E01", ocurrence.Code);
            Assert.Equal("Campo inválido", ocurrence.Message);
            Assert.Equal("seu_numero", ocurrence.Complement);
        }

        [Fact]
        public async Task GetBoletoAsync_On401_ThrowsUnauthorizedException_WhichIsNotAnApiException_AndCarriesNoOcurrences()
        {
            // A real quirk of this codebase: HandleErrors throws Exceptions.Auth.UnauthorizedException directly
            // for 401, bypassing the ApiException branch (and its Ocurrences population) entirely. Every other
            // mapped status goes through ApiException; 401 alone does not, even when the body carries an
            // "ocorrencias" array - it is simply never read for this status.
            const string errorJson = "{\"ocorrencias\":[{\"codigo\":\"E01\",\"mensagem\":\"Token expirado\",\"complemento\":\"\"}]}";
            var (builder, _) = StubClientFactory.CreateBoletoClientBuilder(_ => JsonResponse(HttpStatusCode.Unauthorized, errorJson));

            using var client = builder.Build();

            var exception = await Assert.ThrowsAsync<Exceptions.Auth.UnauthorizedException>(() => client.GetBoletoAsync("0000038806"));

            Assert.IsNotType<ApiException>(exception);
        }

        [Fact]
        public async Task GetBoletoAsync_Throws_ResponseDeserializationException_WhenTheBodyDeserializesToNull()
        {
            var (builder, _) = StubClientFactory.CreateBoletoClientBuilder(_ => JsonResponse(HttpStatusCode.OK, "null"));

            using var client = builder.Build();

            await Assert.ThrowsAsync<ResponseDeserializationException>(() => client.GetBoletoAsync("0000038806"));
        }

        // --- Dispose: ownership of the HttpClient. ---

        [Fact]
        public async Task Dispose_DisposesTheHttpClient_WhenTheClientOwnsIt()
        {
            var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var client = new BoletoClient(new ClientSettings(), new NeverCalledAuthenticator(), httpClient, disposeHttpClient: true);

            client.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => httpClient.GetAsync("https://example.invalid/"));
        }

        [Fact]
        public async Task Dispose_LeavesTheHttpClientUsable_WhenTheClientDoesNotOwnIt()
        {
            var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var httpClient = new HttpClient(stub);
            var client = new BoletoClient(new ClientSettings(), new NeverCalledAuthenticator(), httpClient, disposeHttpClient: false);

            client.Dispose();

            var response = await httpClient.GetAsync("https://example.invalid/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void Dispose_CalledTwice_IsSafe()
        {
            var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var client = new BoletoClient(new ClientSettings(), new NeverCalledAuthenticator(), httpClient, disposeHttpClient: true);

            client.Dispose();
            var exception = Record.Exception(() => client.Dispose());

            Assert.Null(exception);
        }

        /// <summary>An <see cref="Auth.IAuthenticator"/> that fails the test if it is ever invoked - used by the Dispose tests, which never make a request.</summary>
        private sealed class NeverCalledAuthenticator : Auth.IAuthenticator
        {
            public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("Should never be called.");
        }
    }
}
