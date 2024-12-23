
namespace BoletoForBanrisul.Client.Auth
{
    public interface IAuthenticator
    {
        Task<string> GetAccessTokenAsync();
    }
}