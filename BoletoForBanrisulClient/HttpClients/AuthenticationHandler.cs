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
    /// Anexa <c>Authorization: Bearer</c> e <c>bergs-beneficiario</c> em toda requisição. Não existe, no
    /// Banrisul, chamada não autenticada - ao contrário do Santander, não há aqui um mecanismo de skip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ao receber um 401, este handler assume que o token em cache provavelmente expirou (ou foi revogado)
    /// desde a última validação - uma condição plausível e autocurável, não necessariamente credenciais
    /// inválidas - e força exatamente uma renovação (<see cref="IAuthenticator.InvalidateToken"/>), reenviando
    /// a requisição uma única vez com o token novo. Se esse reenvio também vier 401, a resposta é devolvida
    /// como está para quem chamou traduzir em <see cref="Exceptions.Auth.UnauthorizedException"/>; este handler
    /// nunca lança por causa de um status de resposta - quem faz essa tradução é <c>BoletoClient.HandleErrors</c>,
    /// uma camada acima. Capar o retry em uma única tentativa faz uma requisição com credenciais realmente
    /// inválidas falhar rápido em vez de entrar em loop.
    /// </para>
    /// <para>
    /// Isso não compete com a política de retry do Polly em <see cref="PolicyHandler"/>: aquela política só
    /// reage a falhas transitórias de transporte e a 5xx/408 (<c>HandleTransientHttpError</c>), nunca a 401,
    /// então os dois mecanismos de retry nunca disputam a mesma resposta.
    /// </para>
    /// </remarks>
    public class AuthenticationHandler : DelegatingHandler
    {
        private readonly IAuthenticator _authenticator;
        private readonly string _beneficiaryCode;

        /// <summary>Cria o handler.</summary>
        /// <param name="authenticator">Fornece (e pode invalidar) o token de acesso.</param>
        /// <param name="beneficiaryCode">Valor enviado no header <c>bergs-beneficiario</c>.</param>
        /// <param name="innerHandler">O próximo handler da cadeia (tipicamente o handler de transporte).</param>
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

            // O token em cache foi rejeitado: força uma renovação e reenvia exatamente uma vez com um clone
            // da requisição (a HttpRequestMessage original não pode ser enviada mais de uma vez).
            _authenticator.InvalidateToken();

            var retryRequest = HttpRequestCloner.Clone(request, contentBytes, contentHeaders);
            await AttachHeadersAsync(retryRequest, cancellationToken).ConfigureAwait(false);

            response.Dispose();
            return await base.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
        }

        private async Task AttachHeadersAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string token = await _authenticator.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            // Atribuir Authorization substitui o valor anterior; mas bergs-beneficiario não é um header
            // tipado, então precisa ser removido explicitamente antes de reanexado - sem isso, o clone feito
            // pelo PolicyHandler (que copia os headers da requisição original) enviaria o header duplicado em
            // cada tentativa do Polly.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Remove("bergs-beneficiario");
            request.Headers.TryAddWithoutValidation("bergs-beneficiario", _beneficiaryCode);
        }
    }
}
