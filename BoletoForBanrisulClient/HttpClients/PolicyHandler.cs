using Polly;
using System;
using System.Collections.Generic;
using System.Text;

namespace BoletoForBanrisul.Client.HttpClients
{
    public class PolicyHandler : DelegatingHandler
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;

        public PolicyHandler(IAsyncPolicy<HttpResponseMessage> policy, HttpMessageHandler handler) : base(handler)
        {
            _policy = policy;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return await _policy.ExecuteAsync(() => base.SendAsync(request, cancellationToken));
        }
    }
}
