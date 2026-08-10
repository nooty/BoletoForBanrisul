using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BoletoForBanrisul.Client.HttpClients
{
    /// <summary>
    /// Aplica a política do Polly às requisições que podem ser reenviadas com segurança. As demais
    /// seguem direto para o handler interno, enviadas uma única vez.
    /// </summary>
    /// <remarks>
    /// Quem decide o que é seguro reenviar é <see cref="HttpRetryEligibility"/>, e o motivo de a
    /// decisão ser por requisição está documentado lá.
    /// </remarks>
    public class PolicyHandler : DelegatingHandler
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;
        private readonly Func<HttpRequestMessage, bool> _shouldRetry;

        /// <summary>Cria o handler.</summary>
        /// <param name="policy">A política aplicada às requisições elegíveis a retry.</param>
        /// <param name="handler">O próximo handler da cadeia.</param>
        /// <param name="shouldRetry">
        /// Decide se uma requisição pode ser reenviada. Quando omitido, só verbos idempotentes e
        /// requisições explicitamente marcadas como seguras são reenviados.
        /// </param>
        public PolicyHandler(IAsyncPolicy<HttpResponseMessage> policy, HttpMessageHandler handler, Func<HttpRequestMessage, bool>? shouldRetry = null) : base(handler)
        {
            _policy = policy;
            _shouldRetry = shouldRetry ?? HttpRetryEligibility.IsRetrySafe;
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Requisição que não pode ser reenviada não passa pela política: enviar uma única vez
            // dispensa bufferizar o corpo, que no registro é o maior payload da API.
            if (!_shouldRetry(request))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            // Uma HttpRequestMessage não pode ser enviada mais de uma vez; para que o retry
            // funcione, o conteúdo é bufferizado e cada nova tentativa envia um clone da requisição.
            var contentBytes = request.Content != null ? await request.Content.ReadAsByteArrayAsync() : null;
            var contentHeaders = request.Content?.Headers.ToList();

            HttpResponseMessage? previousResponse = null;
            var isFirstAttempt = true;

            // O token vai também para o ExecuteAsync, e não só para o base.SendAsync, para que um
            // cancelamento durante a espera entre tentativas seja observado na hora.
            return await _policy.ExecuteAsync(async attemptCancellationToken =>
            {
                // Descarta a resposta da tentativa anterior que falhou.
                previousResponse?.Dispose();
                previousResponse = null;

                var attemptRequest = isFirstAttempt ? request : HttpRequestCloner.Clone(request, contentBytes, contentHeaders);
                isFirstAttempt = false;

                previousResponse = await base.SendAsync(attemptRequest, attemptCancellationToken);

                return previousResponse;
            }, cancellationToken);
        }
    }
}
