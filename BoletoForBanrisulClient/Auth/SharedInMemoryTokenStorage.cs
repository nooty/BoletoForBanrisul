using System.Collections.Concurrent;

namespace BoletoForBanrisul.Client.Auth
{
    public class SharedInMemoryTokenStorage : ITokenStorage
    {
        // Cache and lock shared by every instance in the process: tokens are reused across
        // distinct clients with the same credentials, avoiding a fresh authentication on
        // every request.
        private static readonly SemaphoreSlim _sharedSemaphore = new SemaphoreSlim(1, 1);
        private static readonly ConcurrentDictionary<string, OAuthToken> _sharedCache = new ConcurrentDictionary<string, OAuthToken>();

        // Safety margin against expiry (5 minutes).
        private static readonly TimeSpan SafetyMargin = TimeSpan.FromMinutes(5);

        public virtual async Task<string> GetOrAddToken(string key, Func<CancellationToken, Task<OAuthToken>> generateNewToken, CancellationToken cancellationToken = default)
        {
            await _sharedSemaphore.WaitAsync(cancellationToken);

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
                _sharedSemaphore.Release();
            }
        }

        public virtual void StoreToken(string key, OAuthToken token)
        {
            _sharedCache[key] = token;
        }

        public virtual bool IsTokenValid(string key)
        {
            var token = GetToken(key);
            return token != null && IsTokenValid(token);
        }

        public virtual void InvalidateToken(string key) => _sharedCache.TryRemove(key, out _);

        // Checks the token against a safety margin, capped at half the token's lifetime so
        // that short-lived tokens are not renewed every time.
        private static bool IsTokenValid(OAuthToken token)
        {
            var lifetime = TimeSpan.FromSeconds(token.ExpiresIn);
            var margin = TimeSpan.FromTicks(Math.Min(SafetyMargin.Ticks, lifetime.Ticks / 2));

            return token.ExpirationTime > DateTimeOffset.UtcNow.Add(margin);
        }

        public virtual OAuthToken? GetToken(string key)
        {
            return _sharedCache.TryGetValue(key, out var token) ? token : null;
        }
    }
}
