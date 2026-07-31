using BoletoForBanrisul.Client.HttpClients;
using BoletoForBanrisul.Client.Tests.TestSupport;
using System.Net;

namespace BoletoForBanrisul.Client.Tests.HttpClients
{
    /// <summary>
    /// Covers only the builder's own wiring (timeout, base address, the <c>Configure</c> callback, and that a
    /// custom handler set via <see cref="ResilientHttpClientBuilder.SetHandler"/> actually receives requests).
    /// The retry policy itself - the interesting part of what <see cref="ResilientHttpClientBuilder.Build"/>
    /// assembles - is already covered thoroughly and quickly in <c>PolicyHandlerTest</c>, which drives
    /// <see cref="BoletoForBanrisul.Client.HttpClients.PolicyHandler"/> directly with a zero-delay retry
    /// policy. <see cref="ResilientHttpClientBuilder"/> hardcodes a real
    /// <c>Backoff.DecorrelatedJitterBackoffV2</c> policy with second-scale delays and no way to inject a
    /// faster one, so exercising an actual retry through it here would only duplicate that coverage while
    /// making the suite slow.
    /// </summary>
    [Trait("Category", "Unit")]
    public class ResilientHttpClientBuilderTests
    {
        [Fact]
        public void Build_WithNoTimeoutSet_DefaultsToOneHundredSeconds()
        {
            using var httpClient = new ResilientHttpClientBuilder().Build();

            Assert.Equal(TimeSpan.FromSeconds(100), httpClient.Timeout);
        }

        [Fact]
        public void Build_SetTimeout_IsAppliedToTheHttpClient()
        {
            using var httpClient = new ResilientHttpClientBuilder()
                .SetTimeout(TimeSpan.FromSeconds(30))
                .Build();

            Assert.Equal(TimeSpan.FromSeconds(30), httpClient.Timeout);
        }

        [Fact]
        public void Build_SetBaseAddress_IsAppliedToTheHttpClient()
        {
            using var httpClient = new ResilientHttpClientBuilder()
                .SetBaseAddress("https://api.banrisul.com.br")
                .Build();

            Assert.Equal(new Uri("https://api.banrisul.com.br"), httpClient.BaseAddress);
        }

        [Fact]
        public void Build_WithNoBaseAddressSet_LeavesItNull()
        {
            using var httpClient = new ResilientHttpClientBuilder().Build();

            Assert.Null(httpClient.BaseAddress);
        }

        [Fact]
        public void Build_Configure_InvokesTheActionWithTheBuiltHttpClient()
        {
            HttpClient? seen = null;

            using var httpClient = new ResilientHttpClientBuilder()
                .Configure(client => seen = client)
                .Build();

            Assert.Same(httpClient, seen);
        }

        [Fact]
        public async Task Build_SetHandler_RoutesRequestsThroughTheGivenHandler()
        {
            var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

            using var httpClient = new ResilientHttpClientBuilder()
                .SetHandler(stub)
                .Build();

            var response = await httpClient.GetAsync("https://example.invalid/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(stub.Requests);
        }
    }
}
