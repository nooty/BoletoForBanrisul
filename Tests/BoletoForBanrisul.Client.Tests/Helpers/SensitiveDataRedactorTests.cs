using BoletoForBanrisul.Client.Helpers;

namespace BoletoForBanrisul.Client.Tests.Helpers
{
    /// <summary><see cref="SensitiveDataRedactor"/> is <c>internal</c>, reachable here via <c>InternalsVisibleTo</c>.</summary>
    [Trait("Category", "Unit")]
    public class SensitiveDataRedactorTests
    {
        [Fact]
        public void RedactAuthorizationHeader_MasksTheAuthorizationValue()
        {
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["Authorization"] = new[] { "Bearer super-secret-token" }
            };

            var redacted = SensitiveDataRedactor.RedactAuthorizationHeader(headers);

            Assert.Equal("***REDACTED***", Assert.Single(redacted["Authorization"]));
        }

        [Fact]
        public void RedactAuthorizationHeader_LeavesOtherHeadersUntouched()
        {
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["Authorization"] = new[] { "Bearer super-secret-token" },
                ["bergs-beneficiario"] = new[] { "0010000001088" },
                ["Content-Type"] = new[] { "application/json" }
            };

            var redacted = SensitiveDataRedactor.RedactAuthorizationHeader(headers);

            Assert.Equal("0010000001088", Assert.Single(redacted["bergs-beneficiario"]));
            Assert.Equal("application/json", Assert.Single(redacted["Content-Type"]));
        }

        [Theory]
        [InlineData("authorization")]
        [InlineData("AUTHORIZATION")]
        [InlineData("Authorization")]
        [InlineData("AuThOrIzAtIoN")]
        public void RedactAuthorizationHeader_MatchesTheHeaderName_CaseInsensitively(string headerName)
        {
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                [headerName] = new[] { "Bearer super-secret-token" }
            };

            var redacted = SensitiveDataRedactor.RedactAuthorizationHeader(headers);

            Assert.Equal("***REDACTED***", Assert.Single(redacted[headerName]));
        }

        [Fact]
        public void RedactAuthorizationHeader_WhenAbsent_ReturnsHeadersUnchanged()
        {
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["Content-Type"] = new[] { "application/json" }
            };

            var redacted = SensitiveDataRedactor.RedactAuthorizationHeader(headers);

            Assert.Single(redacted);
            Assert.Equal("application/json", Assert.Single(redacted["Content-Type"]));
        }
    }
}
