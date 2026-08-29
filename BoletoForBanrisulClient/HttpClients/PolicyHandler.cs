using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BoletoForBanrisul.Client.HttpClients
{
    /// <summary>
    /// Applies the Polly policy to requests that are safe to resend. The rest go straight to the
    /// inner handler, sent a single time.
    /// </summary>
    /// <remarks>
    /// What decides which ones are safe to resend is <see cref="HttpRetryEligibility"/>, and why that
    /// decision is per request is documented there.
    /// </remarks>
    public class PolicyHandler : DelegatingHandler
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;
        private readonly Func<HttpRequestMessage, bool> _shouldRetry;

        /// <summary>Creates the handler.</summary>
        /// <param name="policy">The policy applied to retry-eligible requests.</param>
        /// <param name="handler">The next handler in the chain.</param>
        /// <param name="shouldRetry">
        /// Decides whether a request may be resent. When omitted, only idempotent verbs and requests
        /// explicitly marked as safe are resent.
        /// </param>
        public PolicyHandler(IAsyncPolicy<HttpResponseMessage> policy, HttpMessageHandler handler, Func<HttpRequestMessage, bool>? shouldRetry = null) : base(handler)
        {
            _policy = policy;
            _shouldRetry = shouldRetry ?? HttpRetryEligibility.IsRetrySafe;
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // A request that cannot be resent does not go through the policy: sending it a single
            // time spares buffering the body, which on registration is the API's largest payload.
            if (!_shouldRetry(request))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            // An HttpRequestMessage cannot be sent more than once; for the retry to work, the content
            // is buffered and each new attempt sends a clone of the request.
            var contentBytes = request.Content != null ? await request.Content.ReadAsByteArrayAsync() : null;
            var contentHeaders = request.Content?.Headers.ToList();

            HttpResponseMessage? previousResponse = null;
            var isFirstAttempt = true;

            // The token goes to ExecuteAsync as well, not only to base.SendAsync, so that a
            // cancellation during the wait between attempts is observed right away.
            return await _policy.ExecuteAsync(async attemptCancellationToken =>
            {
                // Discards the response of the previous, failed attempt.
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
