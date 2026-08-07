using BoletoForBanrisul.Report.Exceptions;
using BoletoForBanrisul.Report.Utils;

namespace BoletoForBanrisul.Report.Tests.Utils
{
    /// <summary>
    /// Covers everything <see cref="ChromiumHtmlToPdfConverter"/> does <em>before</em> it launches a
    /// browser - cancellation, the free disk space pre-check, disposal and orphan cleanup. None of it
    /// needs Chrome/Chromium installed, which is fitting: these are the guards that have to work
    /// when the environment is broken.
    /// </summary>
    [Trait("Category", "Unit")]
    public class ChromiumHtmlToPdfConverterTests
    {
        [Fact]
        public async Task ConvertToPdfAsync_WithAnAlreadyCancelledToken_ThrowsBeforeStartingChromium()
        {
            using var converter = new ChromiumHtmlToPdfConverter();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => converter.ConvertToPdfAsync("<html></html>", cts.Token));
        }

        [Fact]
        public async Task ConvertToPdfAsync_WithoutEnoughFreeDiskSpace_ThrowsInsufficientDiskSpace()
        {
            // The threshold is unreachable on purpose: the conversion has to fail on the pre-check,
            // not by hanging inside a Chromium that ran out of disk.
            using var converter = new ChromiumHtmlToPdfConverter(settings =>
                settings.MinimumFreeDiskMegabytes = int.MaxValue);

            var exception = await Assert.ThrowsAsync<InsufficientDiskSpaceException>(
                () => converter.ConvertToPdfAsync("<html></html>"));

            Assert.Equal((long)int.MaxValue * 1024L * 1024L, exception.RequiredBytes);
            Assert.True(exception.FreeBytes < exception.RequiredBytes);
        }

        [Fact]
        public async Task ConvertToPdfAsync_WhenTheDiskCheckFails_LeavesNoProfileBehind()
        {
            var profilesBefore = ExistingProfiles();

            using var converter = new ChromiumHtmlToPdfConverter(settings =>
                settings.MinimumFreeDiskMegabytes = int.MaxValue);

            await Assert.ThrowsAsync<InsufficientDiskSpaceException>(
                () => converter.ConvertToPdfAsync("<html></html>"));

            // Asserting on what is new rather than on the whole set: the first conversion of the
            // process also sweeps orphaned profiles, so the directory can legitimately shrink.
            Assert.Empty(ExistingProfiles().Except(profilesBefore));
        }

        [Fact]
        public async Task ConvertToPdfAsync_AfterDispose_ThrowsObjectDisposed()
        {
            var converter = new ChromiumHtmlToPdfConverter();
            converter.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => converter.ConvertToPdfAsync("<html></html>"));
        }

        [Fact]
        public async Task ConvertToPdfAsync_WithNullHtml_ThrowsArgumentNull()
        {
            using var converter = new ChromiumHtmlToPdfConverter();

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => converter.ConvertToPdfAsync(null!));
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var converter = new ChromiumHtmlToPdfConverter();

            converter.Dispose();
            converter.Dispose();
        }

        [Fact]
        public void Constructor_WithInvalidSettings_Throws()
        {
            // Validated up front so a misconfiguration surfaces where it can be fixed, rather than
            // as a browser that never times out.
            var invalidSettings = new Action<ChromiumConverterSettings>[]
            {
                settings => settings.ConversionTimeout = TimeSpan.Zero,
                settings => settings.ConversionTimeout = TimeSpan.FromSeconds(-1),
                settings => settings.HardTimeoutGrace = TimeSpan.FromSeconds(-1),
                settings => settings.MaxConcurrentConversions = 0,
                settings => settings.MinimumFreeDiskMegabytes = -1,
                settings => settings.WaitForWindowStatusTimeout = -1,
                settings => settings.MediaLoadTimeout = -1,
                settings => settings.OrphanProfileMaxAge = TimeSpan.FromSeconds(-1)
            };

            Assert.All(invalidSettings, makeInvalid =>
                Assert.Throws<ArgumentOutOfRangeException>(() => new ChromiumHtmlToPdfConverter(makeInvalid)));
        }

        [Fact]
        public void Constructor_WithNullSettings_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ChromiumHtmlToPdfConverter((ChromiumConverterSettings)null!));
            Assert.Throws<ArgumentNullException>(() => new ChromiumHtmlToPdfConverter((Action<ChromiumConverterSettings>)null!));
        }

        [Fact]
        public void CleanupOrphanedProfiles_RemovesStaleProfilesAndKeepsRecentOnes()
        {
            var stale = CreateProfileDirectory(DateTime.UtcNow.AddHours(-2));
            var recent = CreateProfileDirectory(DateTime.UtcNow);

            try
            {
                ChromiumHtmlToPdfConverter.CleanupOrphanedProfiles(TimeSpan.FromHours(1));

                // Deleting the whole root would take the recent one with it - in production, the
                // live profile of another process sharing the same temp directory.
                Assert.False(Directory.Exists(stale));
                Assert.True(Directory.Exists(recent));
            }
            finally
            {
                if (Directory.Exists(stale))
                    Directory.Delete(stale, recursive: true);

                if (Directory.Exists(recent))
                    Directory.Delete(recent, recursive: true);
            }
        }

        [Fact]
        public void CleanupOrphanedProfiles_WithNoProfilesRoot_DoesNotThrow()
        {
            // A first run on a clean machine must not fail just because nothing was converted yet.
            ChromiumHtmlToPdfConverter.CleanupOrphanedProfiles(TimeSpan.Zero);
        }

        private static string CreateProfileDirectory(DateTime lastWriteTimeUtc)
        {
            var directory = Path.Combine(ChromiumHtmlToPdfConverter.ProfilesRootDirectory, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);
            Directory.SetLastWriteTimeUtc(directory, lastWriteTimeUtc);

            return directory;
        }

        private static string[] ExistingProfiles()
        {
            var root = ChromiumHtmlToPdfConverter.ProfilesRootDirectory;

            return Directory.Exists(root) ? Directory.GetDirectories(root).OrderBy(x => x).ToArray() : [];
        }
    }
}
