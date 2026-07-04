using BoletoForBanrisul.Client.HttpClients;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [Trait("Category", "Unit")]
    public class PolicyHandlerTest
    {
        [Fact]
        public async Task Should_Retry_Post_With_Body_And_Preserve_Content_And_Headers()
        {
            var attempts = 0;
            var receivedBodies = new List<string>();
            var receivedBeneficiaryHeaders = new List<string?>();
            var receivedContentTypes = new List<string?>();

            var innerHandler = new RequestCapturingHandler(async request =>
            {
                attempts++;
                receivedBodies.Add(request.Content != null ? await request.Content.ReadAsStringAsync() : string.Empty);
                receivedBeneficiaryHeaders.Add(request.Headers.TryGetValues("bergs-beneficiario", out var values) ? values.First() : null);
                receivedContentTypes.Add(request.Content?.Headers.ContentType?.MediaType);

                // Falha com erro transiente nas duas primeiras tentativas.
                if (attempts < 3)
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero });

            using var httpClient = new HttpClient(new PolicyHandler(retryPolicy, innerHandler));

            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/boletos")
            {
                Content = new StringContent("{\"titulo\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("bergs-beneficiario", "0010000001088");

            var response = await httpClient.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, attempts);
            Assert.All(receivedBodies, body => Assert.Equal("{\"titulo\":{}}", body));
            Assert.All(receivedBeneficiaryHeaders, header => Assert.Equal("0010000001088", header));
            Assert.All(receivedContentTypes, contentType => Assert.Equal("application/json", contentType));
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

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero });

            using var httpClient = new HttpClient(new PolicyHandler(retryPolicy, innerHandler));

            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/boletos/123");

            var response = await httpClient.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, attempts);
        }
    }

    public class RequestCapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseGenerator;

        public RequestCapturingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseGenerator)
        {
            _responseGenerator = responseGenerator;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _responseGenerator(request);
        }
    }
}
