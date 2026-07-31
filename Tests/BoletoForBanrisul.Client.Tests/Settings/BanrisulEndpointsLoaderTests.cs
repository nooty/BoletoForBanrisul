using BoletoForBanrisul.Client.Settings;

namespace BoletoForBanrisul.Client.Tests.Settings
{
    /// <summary>
    /// <see cref="BanrisulEndpointsLoader"/> is <c>internal</c> - reachable here only because
    /// <c>InternalsVisibleTo</c> in <c>BoletoForBanrisulClient.csproj</c> names this assembly.
    /// </summary>
    [Trait("Category", "Unit")]
    public class BanrisulEndpointsLoaderTests
    {
        [Fact]
        public void LoadSettings_Production_ReturnsTheProductionBlock_WithHostSet()
        {
            var settings = new BanrisulEndpointsLoader().LoadSettings(ApiHost.Production);

            Assert.Equal(ApiHost.Production, settings.Host);
            Assert.Equal("https://api.banrisul.com.br", settings.BaseUrl);
        }

        [Fact]
        public void LoadSettings_Sandbox_ReturnsTheSandboxBlock_WithHostSet()
        {
            var settings = new BanrisulEndpointsLoader().LoadSettings(ApiHost.Sandbox);

            Assert.Equal(ApiHost.Sandbox, settings.Host);
            Assert.Equal("https://apidev.banrisul.com.br", settings.BaseUrl);
        }

        [Theory]
        [InlineData(ApiHost.Production)]
        [InlineData(ApiHost.Sandbox)]
        public void LoadSettings_HostnameMatchesThePostmanEnvironment(ApiHost host)
        {
            // Docs/Ambiente-Cobrança-v1.6.0.postman_environment.json is the source of truth for these two
            // hostnames; pinned here so a typo in Resources/UrlSettings.json is caught by a unit test rather
            // than by a live request to the wrong host.
            var expectedHost = host == ApiHost.Production ? "api.banrisul.com.br" : "apidev.banrisul.com.br";

            var settings = new BanrisulEndpointsLoader().LoadSettings(host);

            Assert.Equal(expectedHost, new Uri(settings.BaseUrl).Host);
        }

        [Theory]
        [InlineData(ApiHost.Production)]
        [InlineData(ApiHost.Sandbox)]
        public void LoadSettings_NoEndpointPathComesBackEmpty(ApiHost host)
        {
            // This is exactly the failure [JsonPropertyName] on every property exists to prevent: rename a
            // property so it stops matching Resources/UrlSettings.json, and every endpoint silently resolves
            // to "" instead of failing to build.
            var settings = new BanrisulEndpointsLoader().LoadSettings(host);

            var stringProperties = typeof(BanrisulEndpoints).GetProperties()
                .Where(p => p.PropertyType == typeof(string));

            foreach (var property in stringProperties)
            {
                var value = (string?)property.GetValue(settings);
                Assert.False(string.IsNullOrEmpty(value), $"{property.Name} came back empty for {host}.");
            }
        }
    }
}
