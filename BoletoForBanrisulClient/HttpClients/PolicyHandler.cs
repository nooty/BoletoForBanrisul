using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
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
            // Uma HttpRequestMessage não pode ser enviada mais de uma vez; para que o retry
            // funcione, o conteúdo é bufferizado e cada nova tentativa envia um clone da requisição.
            var contentBytes = request.Content != null ? await request.Content.ReadAsByteArrayAsync() : null;
            var contentHeaders = request.Content?.Headers.ToList();

            HttpResponseMessage? previousResponse = null;
            var isFirstAttempt = true;

            return await _policy.ExecuteAsync(async () =>
            {
                // Descarta a resposta da tentativa anterior que falhou.
                previousResponse?.Dispose();
                previousResponse = null;

                var attemptRequest = isFirstAttempt ? request : CloneRequest(request, contentBytes, contentHeaders);
                isFirstAttempt = false;

                previousResponse = await base.SendAsync(attemptRequest, cancellationToken);

                return previousResponse;
            });
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage request, byte[]? contentBytes, List<KeyValuePair<string, IEnumerable<string>>>? contentHeaders)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version
            };

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var property in request.Properties)
            {
                clone.Properties.Add(property.Key, property.Value);
            }

            if (contentBytes != null)
            {
                var content = new ByteArrayContent(contentBytes);

                if (contentHeaders != null)
                {
                    foreach (var header in contentHeaders)
                    {
                        content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                clone.Content = content;
            }

            return clone;
        }
    }
}
