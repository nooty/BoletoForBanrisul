using System;
using System.IO;
using System.Threading.Tasks;

namespace BoletoForBanrisul.IntegrationTests
{
    /// <summary>
    /// Temporary directory for the files a test generates for manual inspection.
    /// </summary>
    /// <remarks>
    /// Writing to a relative path leaves the files in the test output directory forever - the old
    /// <c>Test/bin</c> still carries a hundred of them. Here they live for as long as the test runs
    /// and go away on disposal, unless <see cref="KeepEnvironmentVariable"/> asks otherwise.
    /// </remarks>
    internal sealed class TestArtifactDirectory : IDisposable
    {
        public const string KeepEnvironmentVariable = "KEEP_BANRISUL_TEST_ARTIFACTS";

        private const string RootDirectoryName = "boletoforbanrisul-test-artifacts";

        public TestArtifactDirectory(string name)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                RootDirectoryName,
                $"{name}-{Guid.NewGuid():N}");

            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// Directory holding the artifacts of the current test.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Whether the artifacts survive disposal.
        /// </summary>
        public static bool Keep => IntegrationTestSettings.IsEnabled(KeepEnvironmentVariable);

        /// <summary>
        /// Writes an artifact and returns its full path.
        /// </summary>
        public async Task<string> WriteAsync(string fileName, byte[] content)
        {
            var fullPath = System.IO.Path.Combine(Path, fileName);

            await File.WriteAllBytesAsync(fullPath, content);

            return fullPath;
        }

        public void Dispose()
        {
            if (Keep || !Directory.Exists(Path))
                return;

            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // Losing the cleanup cannot fail a test that already passed; what is left behind is
                // in the machine's temp directory, not in the build output.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
