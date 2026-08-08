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
                settings => settings.OrphanProfileMaxAge = TimeSpan.FromSeconds(-1),
                settings => settings.OrphanCleanupInterval = TimeSpan.FromSeconds(-1)
            };

            Assert.All(invalidSettings, makeInvalid =>
                Assert.Throws<ArgumentOutOfRangeException>(() => new ChromiumHtmlToPdfConverter(makeInvalid)));
        }

        [Fact]
        public void Constructor_WithAnInfiniteCleanupInterval_IsAccepted()
        {
            // Timeout.InfiniteTimeSpan is negative, so it would fall into a naive range check - and
            // it is the one way to ask for the old "sweep once per process" behaviour.
            using var converter = new ChromiumHtmlToPdfConverter(settings =>
                settings.OrphanCleanupInterval = Timeout.InfiniteTimeSpan);
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
                TryDelete(stale);
                TryDelete(recent);
            }
        }

        [Fact]
        public async Task ConvertToPdfAsync_WithADueCleanupInterval_SweepsOrphanedProfilesMoreThanOnce()
        {
            // A zero interval leaves the sweep permanently due, which makes this independent of the
            // order the other tests run in this process.
            using var converter = new ChromiumHtmlToPdfConverter(settings =>
            {
                settings.OrphanCleanupInterval = TimeSpan.Zero;
                settings.MinimumFreeDiskMegabytes = int.MaxValue; // fails before launching Chromium
            });

            // First conversion: guarantees a sweep has already happened in this process.
            await Assert.ThrowsAsync<InsufficientDiskSpaceException>(
                () => converter.ConvertToPdfAsync("<html></html>"));

            // Only now does the stale profile appear. A once-per-process sweep never collects it.
            var stale = CreateProfileDirectory(DateTime.UtcNow.AddHours(-2));

            try
            {
                // The sweep runs in the background and gives up when another one is already in
                // flight, so the conversion is repeated until it picks the profile up.
                Assert.True(
                    await WaitUntilAsync(async () =>
                    {
                        await Assert.ThrowsAsync<InsufficientDiskSpaceException>(
                            () => converter.ConvertToPdfAsync("<html></html>"));

                        return !Directory.Exists(stale);
                    }, TimeSpan.FromSeconds(10)),
                    "The orphan cleanup did not remove a stale profile created after the first sweep.");
            }
            finally
            {
                TryDelete(stale);
            }
        }

        [Fact]
        public async Task ConvertToPdfAsync_WithAnAggressiveOrphanMaxAge_KeepsProfilesYoungerThanTheDeadline()
        {
            // Taken literally, a zero max age asks for every profile to be treated as an orphan. The
            // sweep now runs alongside conversions, so obeying it would delete the profile of a
            // conversion still in flight.
            using var converter = new ChromiumHtmlToPdfConverter(settings =>
            {
                settings.OrphanProfileMaxAge = TimeSpan.Zero;
                settings.OrphanCleanupInterval = TimeSpan.Zero;
                settings.MinimumFreeDiskMegabytes = int.MaxValue;
            });

            var live = CreateProfileDirectory(DateTime.UtcNow);

            try
            {
                // Twice: the first sweep may lose the race to another test's, the second cannot be
                // blamed on that.
                await Assert.ThrowsAsync<InsufficientDiskSpaceException>(
                    () => converter.ConvertToPdfAsync("<html></html>"));

                await Task.Delay(TimeSpan.FromMilliseconds(500));

                await Assert.ThrowsAsync<InsufficientDiskSpaceException>(
                    () => converter.ConvertToPdfAsync("<html></html>"));

                await Task.Delay(TimeSpan.FromMilliseconds(500));

                Assert.True(Directory.Exists(live), "The cleanup deleted a profile younger than the conversion deadline.");
            }
            finally
            {
                TryDelete(live);
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

        private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                if (await condition())
                    return true;

                await Task.Delay(100);
            }

            return false;
        }

        /// <summary>
        /// Deliberately tolerant: a background sweep can delete the directory between the check and
        /// the delete.
        /// </summary>
        private static void TryDelete(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
