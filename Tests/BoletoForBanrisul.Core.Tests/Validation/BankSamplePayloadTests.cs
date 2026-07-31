using BoletoForBanrisul.Core.Converters;
using BoletoForBanrisul.Core.Models;
using BoletoForBanrisul.Core.Validation;
using System.Text.Json;

namespace BoletoForBanrisul.Core.Tests.Validation
{
    /// <summary>
    /// Every request body under "Registrar boleto" &gt; "Cenários de sucesso" in the bank's own sandbox
    /// Postman collection, deserialized and run through <see cref="RecursiveValidator"/>.
    /// </summary>
    /// <remarks>
    /// The bodies are pulled out of <c>Docs/API-Boletos-Banrisul-SandBox-v1.6.0.postman_collection.json</c> at
    /// test time rather than copied into the test project: a copy would drift the moment the bank publishes a
    /// new collection, and the tests would keep passing against a body the bank no longer sends - which is
    /// exactly the failure mode this file exists to catch. This is the test that stops anyone from inventing a
    /// rule the bank does not have: if the local rules ever reject one of the bank's own successful sample
    /// requests, the rule is wrong, not the sample.
    /// </remarks>
    [Trait("Category", "Unit")]
    public class BankSamplePayloadTests
    {
        private const string CollectionFileName = "API-Boletos-Banrisul-SandBox-v1.6.0.postman_collection.json";
        private const string RegistrarBoletoFolder = "Registrar boleto";
        private const string SuccessScenariosFolder = "Cenários de sucesso";

        private static readonly JsonSerializerOptions DeserializeOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = new SnakeCaseLowerNamingPolicy()
        };

        public static IEnumerable<object[]> SuccessScenarioNames() =>
            GetSuccessScenarioNames().Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(SuccessScenarioNames))]
        public void SuccessScenario_ValidatesCleanly(string scenarioName)
        {
            string json = GetSuccessScenarioRequestBody(scenarioName);

            var boleto = JsonSerializer.Deserialize<Boleto>(json, DeserializeOptions);
            Assert.NotNull(boleto);

            bool isValid = RecursiveValidator.TryValidate(boleto, out var errors);

            Assert.True(
                isValid,
                $"Bank sample scenario '{scenarioName}' failed local validation - the bank's own request " +
                $"is authoritative, so a failure here means a validation rule is too strict, not that the " +
                $"sample is wrong:{Environment.NewLine}" +
                string.Join(
                    Environment.NewLine,
                    errors.Select(e => $"  - {e.MemberNames.FirstOrDefault() ?? "(object)"}: {e.ErrorMessage}")));
        }

        [Fact]
        public void SuccessScenarios_NumberSeventeen()
        {
            // Pinned so a change to the bank's collection (a scenario added or removed) is visible even
            // before the Theory above runs - a silently shrinking scenario count would otherwise just mean
            // fewer test cases, not a failure.
            Assert.Equal(17, GetSuccessScenarioNames().Count);
        }

        // --- Reading the committed Postman collection. See BoletoForSantander.Core.Tests.TestAssets for the
        // --- sibling library's version of this same approach. ---

        private static readonly Lazy<string> LazyRepoRoot = new Lazy<string>(FindRepoRoot);

        private static string RepoRoot => LazyRepoRoot.Value;

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BoletoForBanrisul.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                $"Could not locate BoletoForBanrisul.sln walking up from '{AppContext.BaseDirectory}'.");
        }

        private static string CollectionPath => Path.Combine(RepoRoot, "Docs", CollectionFileName);

        private static List<string> GetSuccessScenarioNames() =>
            GetSuccessScenariosFolder().GetProperty("item")
                .EnumerateArray()
                .Select(item => item.GetProperty("name").GetString()!)
                .ToList();

        private static string GetSuccessScenarioRequestBody(string scenarioName)
        {
            var scenario = GetSuccessScenariosFolder().GetProperty("item")
                .EnumerateArray()
                .FirstOrDefault(item => string.Equals(item.GetProperty("name").GetString(), scenarioName, StringComparison.Ordinal));

            if (scenario.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidOperationException(
                    $"Scenario '{scenarioName}' was not found under '{RegistrarBoletoFolder}' > '{SuccessScenariosFolder}'.");
            }

            return scenario.GetProperty("request").GetProperty("body").GetProperty("raw").GetString()!;
        }

        /// <summary>
        /// Locates <c>"Registrar boleto" &gt; "Cenários de sucesso"</c>, scoped rather than searched globally
        /// by name across the whole collection, because folder names like "Cenários de sucesso" repeat under
        /// several other endpoints in the same file.
        /// </summary>
        private static JsonElement GetSuccessScenariosFolder()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(CollectionPath));

            // JsonDocument is disposed at the end of this using block, which would invalidate any JsonElement
            // handed back from it; Clone() detaches the returned element (and everything under it) from the
            // document so callers can keep using it afterwards.
            var root = document.RootElement.Clone();

            var registrarBoleto = root.GetProperty("item")
                .EnumerateArray()
                .FirstOrDefault(item => string.Equals(item.GetProperty("name").GetString(), RegistrarBoletoFolder, StringComparison.Ordinal));

            if (registrarBoleto.ValueKind == JsonValueKind.Undefined)
                throw new InvalidOperationException($"Folder '{RegistrarBoletoFolder}' was not found in '{CollectionFileName}'.");

            var successScenarios = registrarBoleto.GetProperty("item")
                .EnumerateArray()
                .FirstOrDefault(item => string.Equals(item.GetProperty("name").GetString(), SuccessScenariosFolder, StringComparison.Ordinal));

            if (successScenarios.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidOperationException(
                    $"Folder '{SuccessScenariosFolder}' was not found under '{RegistrarBoletoFolder}' in '{CollectionFileName}'.");
            }

            return successScenarios;
        }
    }
}
