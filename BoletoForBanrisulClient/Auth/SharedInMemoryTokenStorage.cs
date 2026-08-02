using System.Collections.Concurrent;

namespace BoletoForBanrisul.Client.Auth
{
    public class SharedInMemoryTokenStorage : ITokenStorage
    {
        // Cache e lock compartilhados entre todas as instâncias do processo: tokens são
        // reaproveitados entre clients distintos com as mesmas credenciais, evitando uma
        // nova autenticação a cada requisição.
        private static readonly SemaphoreSlim _sharedSemaphore = new SemaphoreSlim(1, 1);
        private static readonly ConcurrentDictionary<string, OAuthToken> _sharedCache = new ConcurrentDictionary<string, OAuthToken>();

        // Define a margem de segurança para expiração (5 minutos).
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
            return _sharedCache.TryGetValue(key, out var token) ? token : null;
        }
    }
}
