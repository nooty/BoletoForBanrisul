using BoletoForBanrisul.Client.Auth;
using BoletoForBanrisul.Core.Converters;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Test
{
    [Trait("Category", "Unit")]
    public class AuthenticationConcurrencyTest
    {
        [Fact]
        public async Task Should_Handle_Concurrent_Token_Access_Properly_With_Multiple_Authenticators()
        {
            var tokenStorageSpy = new Mock<InMemoryTokenStorage>() { CallBase = true };

            var settings = CreateAuthenticatorSettings();

            // Cria e executa múltiplas tasks.
            var tasks = CreateAuthenticatorTasks(1000, tokenStorageSpy.Object, settings);

            // Aguarda a execução de todas as tasks.
            var results = await Task.WhenAll(tasks);

            // Verifica se todas as chamadas retornaram o mesmo token de acesso.
            var firstResult = results.First();
            Assert.All(results, result => Assert.Equal(firstResult, result));

            tokenStorageSpy.Verify(s => s.StoreToken(It.IsAny<string>(), It.IsAny<OAuthToken>()), Times.Once);
        }

        private AuthenticatorSettings CreateAuthenticatorSettings()
        {
            return new AuthenticatorSettings
            {
                ClientId = "client_id",
                ClientSecret = "client_secret",
                Scope = "boletos",
                TokenUrl = "https://example.com/token"
            };
        }

        private List<Task<string>> CreateAuthenticatorTasks(int taskCount, ITokenStorage storage, AuthenticatorSettings settings)
        {
            var tasks = new List<Task<string>>();

            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var httpClient = CreateMockedHttpClient();

                    var authenticator = new Authenticator(settings, storage, httpClient);

                    return await authenticator.GetAccessTokenAsync();
                }));
            }

            return tasks;
        }

        private HttpClient CreateMockedHttpClient()
        {
            var mockHttpMessageHandler = new MockHttpMessageHandler(() =>
            {
                var guid = Guid.NewGuid().ToString();
                var newToken = new OAuthToken { AccessToken = $"valid_token_{guid}", ExpiresIn = 3600 };

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(newToken, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = new SnakeCaseLowerNamingPolicy()
                    }))
                };
            });

            return new HttpClient(mockHttpMessageHandler);
        }

    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _responseGenerator;

        public MockHttpMessageHandler(Func<HttpResponseMessage> responseGenerator)
        {
            _responseGenerator = responseGenerator;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return await Task.FromResult(_responseGenerator());
        }
    }
}
