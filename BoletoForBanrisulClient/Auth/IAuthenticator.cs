
namespace BoletoForBanrisul.Client.Auth
{
    public interface IAuthenticator
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Descarta o token em cache para essas credenciais, forçando a próxima chamada a
        /// <see cref="GetAccessTokenAsync"/> a solicitar um novo. Usado pelo pipeline HTTP quando uma
        /// chamada à API retorna 401: o token em cache provavelmente expirou (ou foi revogado) desde a
        /// última validação, então uma única renovação forçada é tentada antes de propagar a falha.
        /// </summary>
        void InvalidateToken();
    }
}