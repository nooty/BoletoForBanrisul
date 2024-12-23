using System;
using System.Runtime.Caching;

namespace BoletoForBanrisul.Client.Auth
{
    public class InMemoryTokenStorage : ITokenStorage
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static readonly MemoryCache _cache = MemoryCache.Default;

        // Define a margem de segurança para expiração (5 minutos).
        private static readonly TimeSpan SafetyMargin = TimeSpan.FromMinutes(5);

        public virtual async Task<string> GetOrAddToken(string key, Func<Task<OAuthToken>> generateNewToken)
        {
            await _semaphore.WaitAsync();

            try
            {
                var token = GetToken(key);

                if (token == null || !IsTokenValid(key))
                {
                    token = await generateNewToken();
                    StoreToken(key, token);
                }

                return token.AccessToken;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public virtual void StoreToken(string key, OAuthToken token)
        {
            var cachePolicy = new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
            };

            _cache.Set(key, token, cachePolicy);
        }

        public virtual bool IsTokenValid(string key)
        {
            var token = GetToken(key);
            if (token == null) return false;

            // Verifica a validade do token com uma margem de segurança.
            return token.ExpirationTime > DateTimeOffset.UtcNow.Add(SafetyMargin);
        }

        public virtual OAuthToken? GetToken(string key)
        {
            return _cache.Get(key) as OAuthToken;
        }
    }
}
