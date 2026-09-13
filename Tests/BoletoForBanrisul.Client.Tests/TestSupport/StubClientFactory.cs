using System.Net;
using System.Text;

namespace BoletoForBanrisul.Client.Tests.TestSupport
{
    /// <summary>
    /// Builds a <see cref="BoletoClient"/> over a single <see cref="StubHttpMessageHandler"/>, wired in via
    /// <see cref="BoletoClientBuilder.SetTransportHandler"/> so no real network call is ever made - not to the
    /// Banrisul API, not to the OAuth token endpoint. The same stub serves both the API HttpClient and the
    /// authenticator's HttpClient, since <see cref="BoletoClientBuilder.Build"/> builds two on top of the
    /// same transport handler, and neither disposes it: the stub outlives the client just like the shared
    /// transport does in production. The stub answers the token exchange itself (see
    /// <see cref="TokenOkResponse"/>) so tests only need to supply a responder for the API call(s) under test.
    /// </summary>
    internal static class StubClientFactory
    {
        /// <summary>A minimal, valid OAuth token response, shaped like <c>Authenticator.RequestNewTokenAsync</c> expects.</summary>
        internal static HttpResponseMessage TokenOkResponse() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"stub-token\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
        };

        /// <summary>Whether a captured request was the OAuth token exchange rather than an API call.</summary>
        internal static bool IsTokenRequest(HttpRequestMessage request) =>
            request.RequestUri!.AbsolutePath.Contains("/oauth/", StringComparison.OrdinalIgnoreCase);

        /// <summary>Every captured request that was not the OAuth token exchange, in order.</summary>
        internal static List<HttpRequestMessage> ApiRequests(this StubHttpMessageHandler stub) =>
            stub.Requests.Where(r => !IsTokenRequest(r)).ToList();

        /// <summary>
        /// Creates a stub handler that answers the token exchange automatically and delegates every other
        /// request to <paramref name="apiResponder"/>.
        /// </summary>
        internal static StubHttpMessageHandler CreateStub(Func<HttpRequestMessage, HttpResponseMessage> apiResponder)
        {
            return new StubHttpMessageHandler(request => IsTokenRequest(request) ? TokenOkResponse() : apiResponder(request));
        }

        /// <summary>Builds a <see cref="BoletoClientBuilder"/> whose HTTP traffic is entirely served by a stub.</summary>
        /// <param name="apiResponder">Answers every non-token request.</param>
        /// <param name="beneficiaryCode">The beneficiary code the builder is configured with.</param>
        /// <param name="clientId">
        /// Unique per test by default: the OAuth token cache (<see cref="Auth.SharedInMemoryTokenStorage"/>) is
        /// process-wide and keyed on a hash of <c>clientId:clientSecret</c>, so reusing a fixed client ID
        /// across tests would let one test's cached token leak into another's assertions.
        /// </param>
        /// <param name="useSandbox">Whether to target the sandbox host.</param>
        internal static (BoletoClientBuilder Builder, StubHttpMessageHandler Stub) CreateBoletoClientBuilder(
            Func<HttpRequestMessage, HttpResponseMessage> apiResponder,
            string beneficiaryCode = "0010000001088",
            string? clientId = null,
            bool useSandbox = true)
        {
            var stub = CreateStub(apiResponder);

            var builder = new BoletoClientBuilder()
                .SetBeneficiary(beneficiaryCode)
                .SetAuthentication(clientId ?? $"client-{Guid.NewGuid()}", "client-secret")
                .SetTransportHandler(stub);

            if (useSandbox)
                builder.UseSandbox();

            return (builder, stub);
        }
    }
}
