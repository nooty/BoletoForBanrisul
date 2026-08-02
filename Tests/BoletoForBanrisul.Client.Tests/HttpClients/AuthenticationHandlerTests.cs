using BoletoForBanrisul.Client.Auth;
using BoletoForBanrisul.Client.HttpClients;
using BoletoForBanrisul.Client.Tests.TestSupport;
using System.Net;

namespace BoletoForBanrisul.Client.Tests.HttpClients
{
    [Trait("Category", "Unit")]
    public class AuthenticationHandlerTests
    {
        private class FakeAuthenticator : IAuthenticator
        {
            public int TokenCallCount { get; private set; }
            public int InvalidateCallCount { get; private set; }
            public string Token { get; private set; } = "token-1";

            public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                TokenCallCount++;
                return Task.FromResult(Token);
            }

            public void InvalidateToken()
            {
                InvalidateCallCount++;
                Token = "token-2";
            }
        }

        [Fact]
        public async Task AttachesBearerAndBeneficiaryHeaders_ToAnOrdinaryRequest()
        {
            var inner = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var fakeAuth = new FakeAuthenticator();
            var handler = new AuthenticationHandler(fakeAuth, "0010000001088", inner);
            var httpClient = new HttpClient(handler);

            var response = await httpClient.GetAsync("https://example.test/boletos");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(inner.Requests);

            var sentRequest = inner.Requests[0];
            Assert.Equal("Bearer", sentRequest.Headers.Authorization!.Scheme);
            Assert.Equal("token-1", sentRequest.Headers.Authorization!.Parameter);
            Assert.Equal("0010000001088", sentRequest.Headers.GetValues("bergs-beneficiario").Single());
        }

        [Fact]
        public async Task RetriesOnce_WithARefreshedToken_WhenTheFirstAttemptReturns401()
        {
            int callCount = 0;
            var inner = new StubHttpMessageHandler(_ =>
            {
                callCount++;
                return new HttpResponseMessage(callCount == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
            });
            var fakeAuth = new FakeAuthenticator();
            var handler = new AuthenticationHandler(fakeAuth, "0010000001088", inner);
            var httpClient = new HttpClient(handler);

            var response = await httpClient.GetAsync("https://example.test/boletos");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, inner.Requests.Count);
            Assert.Equal(1, fakeAuth.InvalidateCallCount);
            Assert.Equal("token-2", inner.Requests[1].Headers.Authorization!.Parameter);
        }

        [Fact]
        public async Task DoesNotRetryMoreThanOnce_WhenTheRetryAlsoReturns401()
        {
            var inner = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
            var fakeAuth = new FakeAuthenticator();
            var handler = new AuthenticationHandler(fakeAuth, "0010000001088", inner);
            var httpClient = new HttpClient(handler);

            var response = await httpClient.GetAsync("https://example.test/boletos");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(2, inner.Requests.Count); // original attempt + exactly one retry, never more.
            Assert.Equal(1, fakeAuth.InvalidateCallCount);
        }

        [Fact]
        public async Task DoesNotDuplicate_TheBeneficiaryHeader_WhenTheRequestAlreadyCarriesIt()
        {
            // Guards the scenario a Polly retry clone produces: PolicyHandler clones the already-decorated
            // request (headers included) before handing it back down the chain, so this handler must remove
            // any pre-existing "bergs-beneficiario" before re-adding it, or the header would be sent twice.
            var inner = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var fakeAuth = new FakeAuthenticator();
            var handler = new AuthenticationHandler(fakeAuth, "0010000001088", inner);
            var httpClient = new HttpClient(handler);

            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/boletos");
            request.Headers.TryAddWithoutValidation("bergs-beneficiario", "already-there");

            await httpClient.SendAsync(request);

            var sentRequest = Assert.Single(inner.Requests);
            Assert.Equal("0010000001088", Assert.Single(sentRequest.Headers.GetValues("bergs-beneficiario")));
        }
    }
}
