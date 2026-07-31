namespace BoletoForBanrisul.Client.Tests.TestSupport
{
    /// <summary>
    /// A fake <see cref="HttpMessageHandler"/> that never touches the network: it records every request it
    /// sees (including the request body, read up front since <see cref="HttpContent"/> can only be read once)
    /// and answers with whatever <see cref="Responder"/> returns.
    /// </summary>
    internal class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        /// <summary>Every request this handler has seen, in order.</summary>
        public List<HttpRequestMessage> Requests { get; } = new();

        /// <summary>The body of each request in <see cref="Requests"/> (by index), or <c>null</c> when the request had no content.</summary>
        public List<string?> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

            return _responder(request);
        }
    }
}
