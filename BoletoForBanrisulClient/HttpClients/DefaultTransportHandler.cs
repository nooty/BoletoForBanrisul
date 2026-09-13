using System;
using System.Net.Http;

namespace BoletoForBanrisul.Client.HttpClients
{
    /// <summary>
    /// Process-wide transport shared by every client, so that building and disposing clients never
    /// opens or closes sockets.
    /// </summary>
    /// <remarks>
    /// Never disposed on purpose, like the static <see cref="HttpClient"/> the .NET guidelines recommend:
    /// idle connections close after 2 minutes, every connection is recycled after 15 minutes (so DNS
    /// changes are picked up) and the OS closes whatever is left when the process exits.
    /// See https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient-guidelines.
    /// </remarks>
    internal static class DefaultTransportHandler
    {
        private static readonly Lazy<StandardSocketsHttpHandler> _instance = new Lazy<StandardSocketsHttpHandler>(() => new StandardSocketsHttpHandler
        {
            UseCookies = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        });

        public static HttpMessageHandler Instance => _instance.Value;
    }
}
