
namespace BoletoForBanrisul.Client.Auth
{
    public interface IAuthenticator
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Discards the cached token for these credentials, forcing the next call to
        /// <see cref="GetAccessTokenAsync"/> to request a new one. Used by the HTTP pipeline when an API
        /// call returns 401: the cached token has probably expired (or been revoked) since the last
        /// validation, so a single forced renewal is attempted before propagating the failure.
        /// </summary>
        void InvalidateToken();
    }
}