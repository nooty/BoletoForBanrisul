namespace BoletoForBanrisul.Client.Tests.TestSupport
{
    /// <summary>
    /// A fake <see cref="HttpMessageHandler"/> that answers every request with whatever
    /// <see cref="_responseGenerator"/> returns, ignoring the request itself. Used where a test only cares
    /// about the response shape (e.g. token exchange) and not about what was sent.
    /// </summary>
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
