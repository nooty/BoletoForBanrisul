namespace BoletoForBanrisul.Client.Tests.TestSupport
{
    /// <summary>
    /// A fake <see cref="HttpMessageHandler"/> that hands each request to <see cref="_responseGenerator"/>,
    /// letting the caller inspect the request (body, headers, ...) before producing a response.
    /// </summary>
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
