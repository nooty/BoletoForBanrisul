using System.Text.RegularExpressions;

namespace BoletoForBanrisul.Client.Helpers
{
    // Auxilia na redação de dados sensíveis (segredos, tokens) antes de gravar logs de requisição/resposta em disco.
    internal static class SensitiveDataRedactor
    {
        private const string RedactedValue = "***REDACTED***";

        public static IDictionary<string, IEnumerable<string>> RedactAuthorizationHeader(IDictionary<string, IEnumerable<string>> headers)
        {
            return headers.ToDictionary(
                h => h.Key,
                h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                    ? new[] { RedactedValue }.AsEnumerable()
                    : h.Value);
        }

        public static string RedactFormUrlEncodedField(string content, string fieldName)
        {
            return Regex.Replace(content, $@"({Regex.Escape(fieldName)}=)[^&]*", $"$1{RedactedValue}");
        }

        public static string RedactJsonField(string content, string fieldName)
        {
            return Regex.Replace(content, $@"(""{Regex.Escape(fieldName)}""\s*:\s*"")[^""]*("")", $"$1{RedactedValue}$2");
        }
    }
}
