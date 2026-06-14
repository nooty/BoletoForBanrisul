using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.Auth
{
    public interface ITokenStorage
    {
        void StoreToken(string key, OAuthToken token);
        OAuthToken? GetToken(string key);
        bool IsTokenValid(string key);
        Task<string> GetOrAddToken(string key, Func<CancellationToken, Task<OAuthToken>> generateNewToken, CancellationToken cancellationToken = default);
    }

}
