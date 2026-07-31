using BoletoForBanrisul.Client.Auth;

namespace BoletoForBanrisul.Client.Tests.TestSupport
{
    /// <summary>
    /// A hand-written <see cref="ITokenStorage"/> double that counts how many times <see cref="StoreToken"/>
    /// is called. Replaces a Moq spy on <see cref="SharedInMemoryTokenStorage"/> (<c>CallBase = true</c>,
    /// asserting <c>StoreToken</c> was called <c>Times.Once</c>).
    /// </summary>
    /// <remarks>
    /// Reproduces just enough of <see cref="SharedInMemoryTokenStorage"/>'s single-flight locking - a
    /// <see cref="SemaphoreSlim"/> guarding a check-then-store - for <see cref="AuthenticationConcurrencyTest"/>
    /// to prove that many concurrent <c>Authenticator.GetAccessTokenAsync</c> calls sharing one instance of
    /// this storage collapse into exactly one call to <see cref="StoreToken"/>. Unlike the production type,
    /// the cache here is per-instance, not static/process-wide - each test creates its own, so there is
    /// nothing to isolate between tests.
    /// </remarks>
    internal sealed class CountingTokenStorage : ITokenStorage
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly Dictionary<string, OAuthToken> _cache = new();
        private int _storeCount;

        /// <summary>How many times <see cref="StoreToken"/> has been called.</summary>
        public int StoreCount => _storeCount;

        public async Task<string> GetOrAddToken(string key, Func<CancellationToken, Task<OAuthToken>> generateNewToken, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                var token = GetToken(key);

                if (token is null || !IsTokenValid(key))
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

        public void StoreToken(string key, OAuthToken token)
        {
            Interlocked.Increment(ref _storeCount);
            _cache[key] = token;
        }

        public OAuthToken? GetToken(string key) => _cache.TryGetValue(key, out var token) ? token : null;

        public bool IsTokenValid(string key) => _cache.ContainsKey(key);
    }
}
