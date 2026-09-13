using BoletoForBanrisul.Client.HttpClients;
using BoletoForBanrisul.Client.Settings;
using BoletoForBanrisul.Client.Tests.TestSupport;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;

namespace BoletoForBanrisul.Client.Tests
{
    [Trait("Category", "Unit")]
    public class BoletoClientBuilderTests
    {
        // --- Guard clauses. ---

        [Fact]
        public void Build_Throws_WhenAuthenticationWasNeverSet()
        {
            var builder = new BoletoClientBuilder().SetBeneficiary("0010000001088");

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Theory]
        [InlineData("", "secret", "boletos")]
        [InlineData("id", "", "boletos")]
        [InlineData("id", "secret", "")]
        public void Build_Throws_WhenAnyAuthenticationFieldIsBlank(string clientId, string clientSecret, string scope)
        {
            var builder = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication(clientId, clientSecret, scope);

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Build_Throws_WhenBeneficiaryCodeIsMissing()
        {
            var builder = new BoletoClientBuilder().SetAuthentication("client-id", "client-secret");

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Build_Throws_WhenLoggingIsEnabled_ButNoLogDirectoryIsSet()
        {
            // EnableLogging(...) always falls back to a default directory (CurrentDirectory/Logs) when none
            // is given, so this guard in LoadClientSettings is not reachable through the builder's public
            // surface as it stands today - only through an internal state combining _enableLogging = true
            // with an empty _logDirectory. Reflection drives exactly that combination so the guard stays
            // covered even though nothing in the current public API can trigger it; see the report for this
            // observation flagged as dead/defensive code rather than fixed, since removing it is out of
            // scope for a test-only change.
            var builder = new BoletoClientBuilder()
                .SetBeneficiary("0010000001088")
                .SetAuthentication("client-id", "client-secret")
                .EnableLogging("some-directory");

            typeof(BoletoClientBuilder)
                .GetField("_logDirectory", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(builder, null);

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        // --- CombineUrl. ---

        [Theory]
        [InlineData("https://api.banrisul.com.br", "cobranca/v1/boletos", "https://api.banrisul.com.br/cobranca/v1/boletos")]
        [InlineData("https://api.banrisul.com.br/", "cobranca/v1/boletos", "https://api.banrisul.com.br/cobranca/v1/boletos")]
        [InlineData("https://api.banrisul.com.br", "cobranca/v1/boletos/{idBoleto}", "https://api.banrisul.com.br/cobranca/v1/boletos/{idBoleto}")]
        public void CombineUrl_JoinsTheBaseAndTheResource(string baseUrl, string resource, string expected)
        {
            var builder = new BoletoClientBuilder();
            var method = typeof(BoletoClientBuilder).GetMethod("CombineUrl", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var combined = (string)method.Invoke(builder, new object[] { baseUrl, resource })!;

            Assert.Equal(expected, combined);
        }

        // --- LoadClientSettings. ---

        [Fact]
        public void LoadClientSettings_ComposesEachUrl_FromBaseUrlAndEndpoint()
        {
            var builder = new BoletoClientBuilder().SetBeneficiary("0010000001088");
            var method = typeof(BoletoClientBuilder).GetMethod("LoadClientSettings", BindingFlags.NonPublic | BindingFlags.Instance)!;

            // SetBeneficiary is still required here: LoadClientSettings guards on it even though the code
            // no longer travels in ClientSettings - it goes straight to the AuthenticationHandler.
            var settings = (ClientSettings)method.Invoke(builder, new object[] { ApiHost.Production })!;

            Assert.Equal("https://api.banrisul.com.br/cobranca/v1/boletos", settings.RegisterBoletoUrl);
            Assert.Equal("https://api.banrisul.com.br/cobranca/v1/boletos/{idBoleto}", settings.GetBoletoUrl);
            Assert.Equal("https://api.banrisul.com.br/cobranca/v1/boletos/{idBoleto}/baixar", settings.CancelBoletoUrl);
            Assert.Equal("https://api.banrisul.com.br/cobranca/v1/boletos/{idBoleto}/emitir", settings.IssueBoletoUrl);
            Assert.Equal("https://api.banrisul.com.br/auth/oauth/v2/token", settings.TokenUrl);
        }

        // --- UseSandbox. ---

        [Fact]
        public async Task Build_UseSandbox_TargetsTheSandboxHost()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"retorno\":\"01\",\"titulo\":{}}", Encoding.UTF8, "application/json")
                },
                useSandbox: true);

            using var client = builder.Build();
            await client.GetBoletoAsync("0000038806");

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal("apidev.banrisul.com.br", sent.RequestUri!.Host);
        }

        [Fact]
        public async Task Build_WithoutUseSandbox_TargetsTheProductionHost()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"retorno\":\"01\",\"titulo\":{}}", Encoding.UTF8, "application/json")
                },
                useSandbox: false);

            using var client = builder.Build();
            await client.GetBoletoAsync("0000038806");

            var sent = Assert.Single(stub.ApiRequests());
            Assert.Equal("api.banrisul.com.br", sent.RequestUri!.Host);
        }

        // --- Transport ownership: the client owns its HttpClients, never the transport underneath. ---

        [Fact]
        public void Dispose_DoesNotDisposeTheTransportHandler()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(_ => new HttpResponseMessage(HttpStatusCode.OK));

            var client = builder.Build();
            client.Dispose();

            Assert.False(stub.Disposed);
        }

        [Fact]
        public async Task Build_Twice_OnTheSameTransport_KeepsWorkingAfterTheFirstClientIsDisposed()
        {
            var (builder, stub) = StubClientFactory.CreateBoletoClientBuilder(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"retorno\":\"01\",\"titulo\":{}}", Encoding.UTF8, "application/json")
                });

            var first = builder.Build();
            first.Dispose();

            using var second = builder.Build();
            await second.GetBoletoAsync("0000038806");

            Assert.Single(stub.ApiRequests());
        }

        [Fact]
        public void CreateTransportHandler_WithoutOverride_ReturnsTheSharedDefaultTransport()
        {
            var builder = new BoletoClientBuilder();
            var method = typeof(BoletoClientBuilder).GetMethod("CreateTransportHandler", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var handler = method.Invoke(builder, Array.Empty<object>());

            Assert.Same(DefaultTransportHandler.Instance, handler);
        }
    }
}
