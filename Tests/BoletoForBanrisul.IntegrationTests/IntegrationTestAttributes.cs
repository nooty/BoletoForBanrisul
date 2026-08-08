using System;

namespace BoletoForBanrisul.IntegrationTests
{
    public sealed class IntegrationFactAttribute : FactAttribute
    {
        public IntegrationFactAttribute()
        {
            if (!IntegrationTestSettings.ShouldRun)
            {
                Skip = IntegrationTestSettings.SkipReason;
            }
        }
    }

    public sealed class IntegrationTheoryAttribute : TheoryAttribute
    {
        public IntegrationTheoryAttribute()
        {
            if (!IntegrationTestSettings.ShouldRun)
            {
                Skip = IntegrationTestSettings.SkipReason;
            }
        }
    }

    internal static class IntegrationTestSettings
    {
        public const string Category = "Integration";
        public const string RunEnvironmentVariable = "RUN_BANRISUL_INTEGRATION_TESTS";
        public const string SkipReason = "Banrisul integration tests are disabled. Set RUN_BANRISUL_INTEGRATION_TESTS=true to run them.";

        public static bool ShouldRun => IsEnabled(RunEnvironmentVariable);

        /// <summary>
        /// Whether an environment variable is set to a truthy value.
        /// </summary>
        public static bool IsEnabled(string variable)
        {
            var value = Environment.GetEnvironmentVariable(variable);

            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
