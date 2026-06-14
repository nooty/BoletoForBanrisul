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

        public virtual async Task<string> GetOrAddToken(string key, Func<CancellationToken, Task<OAuthToken>> generateNewToken, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                var token = GetToken(key);

                if (token == null || !IsTokenValid(token))
                {
                    token = await generateNewToken(cancellationToken);
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
                AbsoluteExpiration = token.ExpirationTime
            };

            _cache.Set(key, token, cachePolicy);
        }

        public virtual bool IsTokenValid(string key)
        {
            var token = GetToken(key);
            return token != null && IsTokenValid(token);
        }

        // Verifica a validade do token com uma margem de segurança, limitada à metade da
        // vida útil do token para que tokens de curta duração não sejam sempre renovados.
        private static bool IsTokenValid(OAuthToken token)
        {
            var lifetime = TimeSpan.FromSeconds(token.ExpiresIn);
            var margin = TimeSpan.FromTicks(Math.Min(SafetyMargin.Ticks, lifetime.Ticks / 2));

            return token.ExpirationTime > DateTimeOffset.UtcNow.Add(margin);
        }

        public virtual OAuthToken? GetToken(string key)
        {
            return _cache.Get(key) as OAuthToken;
        }
    }
}
