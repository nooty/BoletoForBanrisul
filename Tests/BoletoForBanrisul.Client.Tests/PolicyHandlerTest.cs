using BoletoForBanrisul.Client.HttpClients;
using BoletoForBanrisul.Client.Tests.TestSupport;
using Polly;
using Polly.Extensions.Http;
using System.Net;
using System.Text;

namespace BoletoForBanrisul.Client.Tests
{
    [Trait("Category", "Unit")]
    public class PolicyHandlerTest
    {
        /// <summary>
        /// The production policy from <c>ResilientHttpClientBuilder</c> with its second-scale backoff replaced
        /// by zero delays, so these tests exercise the handler's behaviour without paying the real waits.
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> ZeroDelayRetryPolicy() => HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero });

        [Fact]
        public async Task Should_Retry_Post_Marked_RetrySafe_And_Preserve_Content_And_Headers()
        {
            var attempts = 0;
            var receivedBodies = new List<string>();
            var receivedBeneficiaryHeaders = new List<string?>();
            var receivedContentTypes = new List<string?>();
            var receivedRetrySafeMarks = new List<bool>();

            var innerHandler = new RequestCapturingHandler(async request =>
            {
                attempts++;
                receivedBodies.Add(request.Content != null ? await request.Content.ReadAsStringAsync() : string.Empty);
                receivedBeneficiaryHeaders.Add(request.Headers.TryGetValues("bergs-beneficiario", out var values) ? values.First() : null);
                receivedContentTypes.Add(request.Content?.Headers.ContentType?.MediaType);
                receivedRetrySafeMarks.Add(HttpRetryEligibility.IsRetrySafe(request));

                // Fails with a transient error on the first two attempts.
                if (attempts < 3)
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            using var httpClient = new HttpClient(new PolicyHandler(ZeroDelayRetryPolicy(), innerHandler));

            // A neutral URL on purpose: this exercises the marking mechanism and the request cloning around
            // it, not a particular endpoint. In production the OAuth token exchange is the only request that
            // carries this mark - no billing operation does.
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/retry-safe")
            {
                Content = new StringContent("{\"titulo\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("bergs-beneficiario", "0010000001088");
            request.MarkRetrySafe();

            var response = await httpClient.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, attempts);
            Assert.All(receivedBodies, body => Assert.Equal("{\"titulo\":{}}", body));
            Assert.All(receivedBeneficiaryHeaders, header => Assert.Equal("0010000001088", header));
            Assert.All(receivedContentTypes, contentType => Assert.Equal("application/json", contentType));

            // The mark lives in HttpRequestMessage.Properties, which HttpRequestCloner copies - so it has to
            // survive the clone made for every attempt after the first.
            Assert.All(receivedRetrySafeMarks, Assert.True);
        }

        [Fact]
        public async Task Should_Not_Retry_A_Post_That_Was_Not_Marked_RetrySafe()
        {
            // The registration POST is the case this guards: the API documents no idempotency guarantee, so a
            // resend after a lost response would register a second slip. The transient failure is surfaced to
            // the caller instead, exactly once.
            var attempts = 0;

            var innerHandler = new RequestCapturingHandler(request =>
            {
                attempts++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            });

            using var httpClient = new HttpClient(new PolicyHandler(ZeroDelayRetryPolicy(), innerHandler));

            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/boletos")
            {
                Content = new StringContent("{\"titulo\":{}}", Encoding.UTF8, "application/json")
            };

            var response = await httpClient.SendAsync(request);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(1, attempts);
        }

        [Fact]
        public async Task Should_Retry_A_Get_Without_Any_Marking()
        {
            var attempts = 0;

            var innerHandler = new RequestCapturingHandler(request =>
            {
                attempts++;

                return Task.FromResult(new HttpResponseMessage(
                    attempts < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
            });

            using var httpClient = new HttpClient(new PolicyHandler(ZeroDelayRetryPolicy(), innerHandler));

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.com/boletos/123"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task Should_Not_Retry_When_First_Attempt_Succeeds()
        {
            var attempts = 0;

            var innerHandler = new RequestCapturingHandler(request =>
            {
                attempts++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

            using var httpClient = new HttpClient(new PolicyHandler(ZeroDelayRetryPolicy(), innerHandler));

            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/boletos/123");

            var response = await httpClient.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, attempts);
        }
    }
}
