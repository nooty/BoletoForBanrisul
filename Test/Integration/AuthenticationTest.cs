using BoletoForBanrisul.Client;
using BoletoForBanrisul.Client.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [Trait("Category", "Integration")]
    public class AuthenticationTest : AuthenticatedTestBase
    {
        [IntegrationFact]
        public async Task Should_Authenticate_Client()
        {
            var client = (BoletoClient)new BoletoClientBuilder()
                .SetBeneficiary("123456")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var auth = client.Authenticator;

            var token = await auth.GetAccessTokenAsync();

            Assert.NotEmpty(token);
        }

        [IntegrationFact]
        public async Task Should_Create_LogDirectory_With_AuthRequestResponse_Files()
        {
            var LogDirectory = "Logs";

            if (Directory.Exists(LogDirectory))
            {
                Directory.GetFiles(LogDirectory).ToList().ForEach(File.Delete);
                Directory.Delete(LogDirectory);
            }

            var client = (BoletoClient)new BoletoClientBuilder()
                .SetBeneficiary("123456")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .EnableLogging()
                .Build();

            var auth = client.Authenticator;

            // Executa o método de autenticação
            var token = await auth.GetAccessTokenAsync();

            // Verifica se o diretório de logs foi criado
            Assert.True(Directory.Exists(LogDirectory), "Log directory was not created.");

            // Verifica se existem arquivos de log com os prefixos "AuthRequest" e "AuthResponse"
            var logFiles = Directory.GetFiles(LogDirectory, "AuthRequest*").Concat(Directory.GetFiles(LogDirectory, "AuthResponse*")).ToList();

            Assert.NotEmpty(logFiles);
        }

        [IntegrationFact]
        public async Task Should_Save_Token_In_Memory()
        {
            var client = (BoletoClient)new BoletoClientBuilder()
                .SetBeneficiary("123456")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var auth = client.Authenticator;

            var accessToken1 = await auth.GetAccessTokenAsync();
            var accessToken2 = await auth.GetAccessTokenAsync();

            MemoryCache _cache = MemoryCache.Default;

            var tokenInMemory = (OAuthToken)_cache.Get(ComputeTokenKey(ClientId, ClientSecret));

            Assert.Equal(accessToken1, tokenInMemory.AccessToken);
            Assert.Equal(accessToken2, tokenInMemory.AccessToken);
        }

        [IntegrationFact]
        public async Task Should_Reuse_Token_From_Memory_After_BoletoClient_Build()
        {
            var client1 = (BoletoClient)new BoletoClientBuilder()
                .SetBeneficiary("123456")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var auth1 = client1.Authenticator;

            var accessToken1 = await auth1.GetAccessTokenAsync();

            MemoryCache _cache = MemoryCache.Default;
            var tokenInMemory = (OAuthToken)_cache.Get(ComputeTokenKey(ClientId, ClientSecret));

            var client2 = (BoletoClient)new BoletoClientBuilder()
                .SetBeneficiary("123456")
                .SetAuthentication(ClientId, ClientSecret)
                .UseSandbox()
                .Build();

            var auth2 = client2.Authenticator;

            var accessToken2 = await auth2.GetAccessTokenAsync();

            Assert.Equal(accessToken1, tokenInMemory.AccessToken);
            Assert.Equal(accessToken2, tokenInMemory.AccessToken);
        }

        // Replica o cálculo de chave de cache usado por Authenticator.ComputeTokenKey.
        private static string ComputeTokenKey(string clientId, string clientSecret)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
