using BoletoForBanrisul.Report.Exceptions;
using BoletoForBanrisul.Report.Utils;
using ChromiumHtmlToPdfLib.Exceptions;
using System.Diagnostics;

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
                settings => settings.OrphanCleanupInterval = TimeSpan.FromSeconds(-1),
                settings => settings.BrowserStartupRetries = -1
            };

            Assert.All(invalidSettings, makeInvalid =>
                Assert.Throws<ArgumentOutOfRangeException>(() => new ChromiumHtmlToPdfConverter(makeInvalid)));
        }

        [Fact]
        public void Constructor_WithNullPageSettings_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ChromiumHtmlToPdfConverter(settings => settings.PageSettings = null!));
        }

        [Fact]
        public void Constructor_WithASubSecondConversionTimeout_Throws()
        {
            // A biblioteca recusa qualquer coisa até 1 ms, mas o piso útil é outro: abaixo de um
            // segundo o Chromium nem termina de subir, então o valor só consegue produzir falha - e
            // descobrir isso na construção é melhor do que descobrir em produção.
            var tooShort = new[] { TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(999) };

            Assert.All(tooShort, timeout =>
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new ChromiumHtmlToPdfConverter(settings => settings.ConversionTimeout = timeout)));
        }

        [Fact]
        public void Constructor_WithADeadlineBeyondTheSupportedRange_Throws()
        {
            // O timeout vira int de milissegundos na chamada da biblioteca e o deadline duro vira um
            // CancelAfter: passar de int.MaxValue ms trunca no cast ou explode lá dentro. O par no
            // fim é o caso em que nenhuma parcela sozinha passa do teto, mas a soma passa - e é onde
            // uma validação escrita como soma estouraria o próprio TimeSpan antes de comparar.
            var invalidSettings = new Action<ChromiumConverterSettings>[]
            {
                settings => settings.ConversionTimeout = TimeSpan.FromDays(30),
                settings => settings.ConversionTimeout = TimeSpan.MaxValue,
                settings => settings.HardTimeoutGrace = TimeSpan.FromDays(30),
                settings => settings.HardTimeoutGrace = TimeSpan.MaxValue,
                settings =>
                {
                    settings.ConversionTimeout = TimeSpan.FromDays(24);
                    settings.HardTimeoutGrace = TimeSpan.FromDays(2);
                }
            };

            Assert.All(invalidSettings, makeInvalid =>
                Assert.Throws<ArgumentOutOfRangeException>(() => new ChromiumHtmlToPdfConverter(makeInvalid)));
        }

        [Fact]
        public void Constructor_WithTheMaximumSupportedDeadline_IsAccepted()
        {
            // O limite pelo outro lado, para a subtração da validação não ficar errada por um tick.
            var maximumDeadline = TimeSpan.FromMilliseconds(int.MaxValue);

            using var converter = new ChromiumHtmlToPdfConverter(settings =>
            {
                settings.ConversionTimeout = maximumDeadline - TimeSpan.FromMinutes(1);
                settings.HardTimeoutGrace = TimeSpan.FromMinutes(1);
            });
        }

        [Fact]
        public void Constructor_WithASharedProfileAndConcurrentConversions_Throws()
        {
            // Um --user-data-dir não sustenta dois Chromium ao mesmo tempo: o segundo morre no
            // SingletonLock ou repassa o trabalho ao primeiro e sai, devolvendo PDF vazio sem erro.
            Assert.Throws<ArgumentException>(() => new ChromiumHtmlToPdfConverter(settings =>
            {
                settings.UserProfileDirectory = Path.Combine(Path.GetTempPath(), "perfil-compartilhado");
                settings.MaxConcurrentConversions = 2;
            }));
        }

        [Fact]
        public void Constructor_WithASharedProfileAndASingleConversion_IsAccepted()
        {
            // O outro lado da regra: profile compartilhado continua sendo uma escolha válida, desde
            // que serializada.
            using var converter = new ChromiumHtmlToPdfConverter(settings =>
            {
                settings.UserProfileDirectory = Path.Combine(Path.GetTempPath(), "perfil-compartilhado");
                settings.MaxConcurrentConversions = 1;
            });
        }

        [Fact]
        public void Constructor_WithAUserDataDirArgument_Throws()
        {
            // The converter appends its own --user-data-dir after ChromiumArguments precisely so it
            // always has the last word; an entry here would be silently overwritten at best, and at
            // worst point Chromium at a directory the converter does not own, lock or clean up.
            Assert.Throws<ArgumentException>(() => new ChromiumHtmlToPdfConverter(settings =>
                settings.ChromiumArguments.Add("--user-data-dir=/tmp/algum-lugar")));
        }

        [Fact]
        public void Constructor_WithAnInfiniteCleanupInterval_IsAccepted()
        {
            // Timeout.InfiniteTimeSpan is negative, so it would fall into a naive range check - and
            // it is the one way to ask for "sweep once per process".
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
            // sweep runs alongside conversions, so obeying it would delete the profile of a conversion
            // still in flight.
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

        /// <summary>
        /// A negative age is nonsense input, and must not become a way around the owner lock.
        /// </summary>
        /// <remarks>
        /// It is worth being precise about what a negative age does and does not break. It pushes the
        /// cutoff into the future, so every profile becomes a candidate - but so does
        /// <see cref="TimeSpan.Zero"/>, which is a legitimate argument callers do pass. What protects
        /// a conversion in flight is never the age, it is the lock its owner holds. So the invariant
        /// under test is not "fresh profiles survive" - they do not, and should not - but "nonsense
        /// input still cannot delete a profile that has an owner".
        /// </remarks>
        [Fact]
        public void CleanupOrphanedProfiles_WithANegativeAge_StillHonoursTheOwnerLock()
        {
            var live = CreateProfileDirectory(DateTime.UtcNow);

            try
            {
                using (HoldOwnerLock(live))
                {
                    ChromiumHtmlToPdfConverter.CleanupOrphanedProfiles(TimeSpan.FromHours(-1));

                    Assert.True(Directory.Exists(live), "A negative age got past the owner lock.");
                }
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

        [Fact]
        public void CleanupOrphanedProfiles_WithAHeldOwnerLock_KeepsTheStaleProfile()
        {
            // Age is only the trigger. The directory looks abandoned by every date-based measure, and
            // the held lock is the one piece of evidence that says otherwise - which is exactly the
            // case of a neighbouring process configured with a longer deadline than ours.
            var stale = CreateProfileDirectory(DateTime.UtcNow.AddHours(-2));

            try
            {
                // The assertion is on this file rather than on the directory, because the directory
                // survives either way and would make the test vacuous: on Windows a recursive delete
                // erases everything it can and only then fails on the locked file, leaving the
                // profile present but gutted. The name sorts before ".owner.lock" so a sweep that
                // ignored the veto would reach it first. On Linux there is no partial anything -
                // unlink ignores flock and the whole profile goes.
                var content = Path.Combine(stale, "!conteudo-do-chromium.txt");

                File.WriteAllText(content, "dados de uma conversão em andamento");

                using (HoldOwnerLock(stale))
                {
                    ChromiumHtmlToPdfConverter.CleanupOrphanedProfiles(TimeSpan.Zero);

                    Assert.True(File.Exists(content), "The cleanup went into a profile whose owner still held its lock.");
                }
            }
            finally
            {
                TryDelete(stale);
            }
        }

        [Fact]
        public void CleanupOrphanedProfiles_WithAReleasedOwnerLock_RemovesTheStaleProfile()
        {
            // The other half of the contract: the lock is a veto for as long as it is held, not
            // permanent immunity. A released handle is indistinguishable from the handle of an owner
            // the OS reaped, which is the guarantee the whole scheme rests on.
            var stale = CreateProfileDirectory(DateTime.UtcNow.AddHours(-2));

            try
            {
                HoldOwnerLock(stale).Dispose();

                ChromiumHtmlToPdfConverter.CleanupOrphanedProfiles(TimeSpan.Zero);

                Assert.False(Directory.Exists(stale));
            }
            finally
            {
                TryDelete(stale);
            }
        }

        [Fact]
        public void CleanupOrphanedProfiles_WithoutAnOwnerLock_StillUsesTheAgeCriterion()
        {
            // A profile can have no lock file at all. Reading that absence as "in use" would strand it
            // forever - the failure mode of getting the FileNotFoundException/IOException catch order
            // backwards.
            var stale = CreateProfileDirectory(DateTime.UtcNow.AddHours(-2));

            try
            {
                Assert.False(File.Exists(Path.Combine(stale, ChromiumHtmlToPdfConverter.OwnerLockFileName)));

                ChromiumHtmlToPdfConverter.CleanupOrphanedProfiles(TimeSpan.FromHours(1));

                Assert.False(Directory.Exists(stale));
            }
            finally
            {
                TryDelete(stale);
            }
        }

        [Fact]
        public async Task ConvertToPdfAsync_WhenTheConverterCannotStart_LeavesNoProfileBehind()
        {
            var profilesBefore = ExistingProfiles();

            // A broken environment is the point. An executable that is not there fails in the library's
            // constructor - it checks File.Exists and throws FileNotFoundException - so this pins two
            // things at once: the caller gets the documented exception type rather than the raw one,
            // and the profile directory created moments earlier does not stay on disk.
            using var converter = new ChromiumHtmlToPdfConverter(settings =>
            {
                settings.ChromiumExecutablePath = Path.Combine(Path.GetTempPath(), "nao-existe", "chrome.exe");
                settings.MinimumFreeDiskMegabytes = 0; // the disk check must not short-circuit this
                settings.ConversionTimeout = TimeSpan.FromSeconds(1);
                settings.HardTimeoutGrace = TimeSpan.FromSeconds(1);
            });

            await Assert.ThrowsAsync<PdfConversionException>(() => converter.ConvertToPdfAsync("<html></html>"));

            Assert.Empty(ExistingProfiles().Except(profilesBefore));
        }

        /// <summary>
        /// The type-based fallback recognises the library's own conversion timeout, and nothing else.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Pinned here because it is a policy, and policies get "corrected" later. A library conversion
        /// timeout has to be distinguishable from a generic failure, or the documented retry policy
        /// retries a browser that has just wedged.
        /// </para>
        /// <para>
        /// Note what is deliberately <em>not</em> here: cancellation types. The budget never reports
        /// itself that way - the converter's own token does - and accepting them would swallow
        /// unrelated cancellations, such as the HttpClient timeout the library raises while loading
        /// images, telling the caller to wait out a wedged browser over a picture that failed to
        /// download.
        /// </para>
        /// </remarks>
        [Fact]
        public void IsLibraryConversionTimeout_RecognisesOnlyTheLibrarysConversionTimeout()
        {
            var budgetTimeouts = new[]
            {
                CreateLibraryException("ConversionTimedOutException")
            };

            var failures = new[]
            {
                new OperationCanceledException(),
                new TaskCanceledException(),
                CreateLibraryException("ChromiumException"),
                CreateLibraryException("TaskTimedOutException"),
                CreateLibraryException("ConversionException"),
                CreateLibraryException("ChromiumNavigationException"),
                new IOException(),
                new NullReferenceException()
            };

            Assert.All(budgetTimeouts, exception =>
                Assert.True(ChromiumHtmlToPdfConverter.IsLibraryConversionTimeout(exception),
                    $"{exception.GetType().Name} is the library reporting its own conversion timeout."));

            Assert.All(failures, exception =>
                Assert.False(ChromiumHtmlToPdfConverter.IsLibraryConversionTimeout(exception),
                    $"{exception.GetType().Name} is not the library's conversion timeout; the converter's token decides, not the type."));
        }

        /// <summary>
        /// The startup-failure classifier accepts only what the catch ladder can actually see once a
        /// browser fails to start, and nothing that belongs to a different clause.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Mirrors <see cref="IsLibraryConversionTimeout_RecognisesOnlyTheLibrarysConversionTimeout"/>:
        /// this is a policy pinned in one place, because a policy left implicit gets "corrected" the
        /// next time somebody reads the code without the context of why it is narrow.
        /// </para>
        /// <para>
        /// <see cref="ConversionTimedOutException"/> and cancellation types are deliberately among the
        /// rejects: the catch ladder tries the budget/deadline/cancellation clauses first, so by the
        /// time this predicate would run on one of those, the exception has already left as
        /// <see cref="TimeoutException"/> or <see cref="OperationCanceledException"/>. Accepting them
        /// here as well would not change behaviour through the ladder, but it would make the predicate
        /// lie about what it actually classifies if read on its own - which is the failure mode this
        /// test exists to catch.
        /// </para>
        /// </remarks>
        [Fact]
        public void IsTransientBrowserStartupFailure_RecognisesOnlyStartupFailures()
        {
            var startupFailures = new[]
            {
                CreateLibraryException("ChromiumException"),
                new IndexOutOfRangeException()
            };

            var failures = new[]
            {
                CreateLibraryException("ConversionTimedOutException"),
                CreateLibraryException("ConversionException"),
                CreateLibraryException("ChromiumNavigationException"),
                CreateLibraryException("TaskTimedOutException"),
                new OperationCanceledException(),
                new TaskCanceledException(),
                new IOException()
            };

            Assert.All(startupFailures, exception =>
                Assert.True(ChromiumHtmlToPdfConverter.IsTransientBrowserStartupFailure(exception),
                    $"{exception.GetType().Name} is a browser startup failure and should be retried."));

            Assert.All(failures, exception =>
                Assert.False(ChromiumHtmlToPdfConverter.IsTransientBrowserStartupFailure(exception),
                    $"{exception.GetType().Name} is not a startup failure and must not be retried as one."));
        }

        /// <summary>
        /// Mutating the caller's settings after construction must not reach the converter.
        /// </summary>
        /// <remarks>
        /// White-box, like the semaphore test, and for the same reason: what needs pinning is that the
        /// converter reads its own copy, and no observable behaviour distinguishes "read the frozen
        /// copy" from "read the original" without actually launching a browser. Asserting through a
        /// conversion would not do it - the ones that fail without Chrome fail on the disk pre-check,
        /// before any of these settings is read.
        /// </remarks>
        [Fact]
        public void Settings_AreFrozenAtConstruction()
        {
            var settings = new ChromiumConverterSettings
            {
                ConversionTimeout = TimeSpan.FromSeconds(30),
                MaxConcurrentConversions = 1,
                BrowserStartupRetries = 3
            };

            settings.ChromiumArguments.Clear();
            settings.ChromiumArguments.Add("--argumento-original");

            using var converter = new ChromiumHtmlToPdfConverter(settings);

            // Every one of these would be a problem if the converter kept reading the caller's object:
            // the timeout overflows the cast to int, the shared profile contradicts the semaphore's
            // capacity, a null PageSettings blows up inside the library, and a negative retry count
            // would slip past Validate entirely.
            settings.ConversionTimeout = TimeSpan.MaxValue;
            settings.UserProfileDirectory = "perfil-compartilhado";
            settings.MaxConcurrentConversions = 8;
            settings.BrowserStartupRetries = -1;
            settings.PageSettings = null!;
            settings.ChromiumArguments.Clear();
            settings.ChromiumArguments.Add("--isto-nao-deve-chegar-ao-navegador");

            var frozen = GetFrozenSettings(converter);

            Assert.Equal(TimeSpan.FromSeconds(30), frozen.ConversionTimeout);
            Assert.True(string.IsNullOrEmpty(frozen.UserProfileDirectory));
            Assert.Equal(1, frozen.MaxConcurrentConversions);
            Assert.Equal(3, frozen.BrowserStartupRetries);
            Assert.NotNull(frozen.PageSettings);
            Assert.Equal(new[] { "--argumento-original" }, frozen.ChromiumArguments);
        }

        [Fact]
        public void Settings_PageSettingsAreClonedNotShared()
        {
            // PageSettings is the one setting that is a mutable object rather than a value, so copying
            // the rest and keeping this by reference would leave a hole. The library's own ICloneable
            // does the copying - including PaperFormat, whose setter is private and which a hand
            // written copy here would silently drop.
            var settings = new ChromiumConverterSettings();

            settings.PageSettings.MarginTop = 1.5;

            using var converter = new ChromiumHtmlToPdfConverter(settings);

            settings.PageSettings.MarginTop = 9.9;

            Assert.Equal(1.5, GetFrozenSettings(converter).PageSettings.MarginTop);
        }

        [Fact]
        public void NewDisposableProfileName_IsAttributableAndPathSafe()
        {
            var name = ChromiumHtmlToPdfConverter.NewDisposableProfileName();

            using var current = Process.GetCurrentProcess();

            // The pid is there so a leftover directory can be traced back to its owner - never to
            // infer that the owner is still alive, which is what the lock file is for.
            Assert.StartsWith($"p{current.Id}-", name);
            Assert.Equal(32, name.Split('-')[1].Length);
            Assert.Equal(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()));
            Assert.NotEqual(name, ChromiumHtmlToPdfConverter.NewDisposableProfileName());
        }

        /// <summary>
        /// The guarantee behind <c>Dispose</c> not disposing the semaphore, asserted directly.
        /// </summary>
        /// <remarks>
        /// White-box on purpose. The behaviour worth pinning is that an in-flight conversion's
        /// <c>Release()</c> still works after <c>Dispose</c>, and there is no way to prove a
        /// conversion was inside the semaphore at that moment without an internal seam for faking the
        /// browser session - which was deliberately not introduced. So the test asserts the invariant
        /// the fix rests on instead of trying to win a race: the semaphore outlives the disposal.
        /// </remarks>
        [Fact]
        public void Dispose_DoesNotDisposeTheConversionSemaphore()
        {
            var converter = new ChromiumHtmlToPdfConverter();

            converter.Dispose();

            var field = typeof(ChromiumHtmlToPdfConverter)
                .GetField("_conversionSemaphore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(field);

            var semaphore = Assert.IsType<SemaphoreSlim>(field!.GetValue(converter));

            // Both halves of what a conversion in flight does on its way out. A disposed SemaphoreSlim
            // throws ObjectDisposedException on either, swallowing the PDF that was already produced -
            // or, worse, the exception that explained the failure.
            Assert.True(semaphore.Wait(0));
            semaphore.Release();
        }

        /// <summary>
        /// Hunts for the disposal race against real conversions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>This test is probabilistic, and reading it as a guarantee is a mistake.</strong>
        /// Nothing synchronises the <c>Task.Run</c> with the <c>Dispose()</c> that follows it: the
        /// conversion may not have entered the converter at all, take the <c>ObjectDisposedException</c>
        /// that <c>ThrowIfDisposed</c> correctly throws at the door, and leave the race unexercised.
        /// The 200 iterations lower the odds of that happening every time; they do not create
        /// ordering. It can find a regression, never prove the absence of one.
        /// </para>
        /// <para>
        /// The guarantee lives in <see cref="Dispose_DoesNotDisposeTheConversionSemaphore"/>. This one
        /// exists to cover what that cannot reach - the <c>Release()</c> in a real conversion's
        /// <c>finally</c>. Making it deterministic would take an internal seam for faking the browser
        /// session, the same seam whose absence leaves the hard-deadline branch of
        /// <c>AwaitWithHardDeadline</c> untested. Both gaps have that one cause.
        /// </para>
        /// </remarks>
        [Fact]
        public async Task Dispose_RacedAgainstConversions_NeverSurfacesADisposedSemaphore()
        {
            // The disk check throws from inside the try, so the finally that releases the semaphore
            // runs - the exact path that turned a disposed semaphore into an ObjectDisposedException
            // on top of the real result.
            var observed = new List<Exception>();

            for (var attempt = 0; attempt < 200; attempt++)
            {
                var converter = new ChromiumHtmlToPdfConverter(settings =>
                    settings.MinimumFreeDiskMegabytes = int.MaxValue);

                var conversion = Task.Run(async () =>
                {
                    try
                    {
                        await converter.ConvertToPdfAsync("<html></html>");
                    }
                    catch (Exception exception)
                    {
                        lock (observed)
                        {
                            observed.Add(exception);
                        }
                    }
                });

                converter.Dispose();

                await conversion;
            }

            // ObjectDisposedException is a legitimate answer here - the converter really was disposed
            // mid-flight. What must never happen is it coming from the semaphore, which would mean
            // the conversion's own result was swallowed on the way out. SemaphoreSlim's carries no
            // ObjectName, so the race can only hide the bug, never invent one.
            Assert.All(observed.OfType<ObjectDisposedException>(), exception =>
                Assert.Equal(nameof(ChromiumHtmlToPdfConverter), exception.ObjectName));
        }

        private static string CreateProfileDirectory(DateTime lastWriteTimeUtc)
        {
            var directory = Path.Combine(ChromiumHtmlToPdfConverter.ProfilesRootDirectory, Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);
            Directory.SetLastWriteTimeUtc(directory, lastWriteTimeUtc);

            return directory;
        }

        private static ChromiumConverterSettings GetFrozenSettings(ChromiumHtmlToPdfConverter converter)
        {
            var field = typeof(ChromiumHtmlToPdfConverter)
                .GetField("_settings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(field);

            return Assert.IsType<ChromiumConverterSettings>(field!.GetValue(converter));
        }

        /// <summary>
        /// Takes the owner lock of a profile the way a conversion in flight does.
        /// </summary>
        /// <remarks>
        /// FileShare.None on both sides is the whole mechanism: on Windows the kernel enforces the
        /// share mode, on Linux FileStream turns it into flock(LOCK_EX). A FileShare.Read here would
        /// take LOCK_SH instead and stop conflicting with the owner - on Linux only.
        /// </remarks>
        private static FileStream HoldOwnerLock(string profileDirectory)
        {
            var lockPath = Path.Combine(profileDirectory, ChromiumHtmlToPdfConverter.OwnerLockFileName);

            return new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, bufferSize: 1);
        }

        /// <summary>
        /// Builds one of the conversion library's exceptions.
        /// </summary>
        /// <remarks>
        /// The types are public but every constructor is internal to the library, so there is no way
        /// to hand the real thing to the classifier without reflection - and testing the classifier
        /// against stand-ins would test nothing, since it classifies by type. A signature change
        /// fails this loudly, which is the right signal: it means the mapping needs revisiting.
        /// </remarks>
        private static Exception CreateLibraryException(string typeName)
        {
            var type = typeof(ConversionTimedOutException).Assembly.GetType($"ChromiumHtmlToPdfLib.Exceptions.{typeName}");

            Assert.NotNull(type);

            var exception = Activator.CreateInstance(
                type!,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: new object[] { "teste" },
                culture: null) as Exception;

            Assert.NotNull(exception);

            return exception!;
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
