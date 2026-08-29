using BoletoForBanrisul.Client.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.HttpClients
{
    /// <summary>
    /// Attaches <c>Authorization: Bearer</c> and <c>bergs-beneficiario</c> to every request. At Banrisul
    /// there is no unauthenticated call - unlike Santander, there is no skip mechanism here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On a 401, this handler assumes the cached token has probably expired (or been revoked) since the
    /// last validation - a plausible, self-healing condition, not necessarily invalid credentials - and
    /// forces exactly one renewal (<see cref="IAuthenticator.InvalidateToken"/>), resending the request a
    /// single time with the new token. If that resend also comes back 401, the response is returned as-is
    /// for the caller to translate into <see cref="Exceptions.Auth.UnauthorizedException"/>; this handler
    /// never throws over a response status - that translation belongs to <c>BoletoClient.HandleErrors</c>,
    /// one layer above. Capping the retry at a single attempt makes a request with genuinely invalid
    /// credentials fail fast instead of looping.
    /// </para>
    /// <para>
    /// This does not compete with the Polly retry policy in <see cref="PolicyHandler"/>: that policy only
    /// reacts to transient transport failures and to 5xx/408 (<c>HandleTransientHttpError</c>), never to
    /// 401, so the two retry mechanisms never contend over the same response.
    /// </para>
    /// </remarks>
    public class AuthenticationHandler : DelegatingHandler
    {
        private readonly IAuthenticator _authenticator;
        private readonly string _beneficiaryCode;

        /// <summary>Creates the handler.</summary>
        /// <param name="authenticator">Supplies (and can invalidate) the access token.</param>
        /// <param name="beneficiaryCode">Value sent in the <c>bergs-beneficiario</c> header.</param>
        /// <param name="innerHandler">The next handler in the chain (typically the transport handler).</param>
        public AuthenticationHandler(IAuthenticator authenticator, string beneficiaryCode, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            _beneficiaryCode = beneficiaryCode ?? throw new ArgumentNullException(nameof(beneficiaryCode));
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[]? contentBytes = request.Content != null ? await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false) : null;
            List<KeyValuePair<string, IEnumerable<string>>>? contentHeaders = request.Content?.Headers.ToList();

            await AttachHeadersAsync(request, cancellationToken).ConfigureAwait(false);

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            // The cached token was rejected: force a renewal and resend exactly once with a clone of the
            // request (the original HttpRequestMessage cannot be sent more than once).
            _authenticator.InvalidateToken();

            var retryRequest = HttpRequestCloner.Clone(request, contentBytes, contentHeaders);
            await AttachHeadersAsync(retryRequest, cancellationToken).ConfigureAwait(false);

            response.Dispose();
            return await base.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
        }

        private async Task AttachHeadersAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string token = await _authenticator.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            // Assigning Authorization replaces the previous value; but bergs-beneficiario is not a typed
            // header, so it has to be removed explicitly before being reattached - without that, the clone
            // made by PolicyHandler (which copies the original request's headers) would send the header
            // duplicated on every Polly attempt.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Remove("bergs-beneficiario");
            request.Headers.TryAddWithoutValidation("bergs-beneficiario", _beneficiaryCode);
        }
    }
}
