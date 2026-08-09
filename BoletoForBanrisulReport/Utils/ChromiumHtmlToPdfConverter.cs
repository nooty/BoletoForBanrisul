using BoletoForBanrisul.Report.Exceptions;
using ChromiumHtmlToPdfLib;
using ChromiumHtmlToPdfLib.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;

namespace BoletoForBanrisul.Report.Utils
{
    /// <summary>
    /// Converts HTML to PDF using headless Chrome/Chromium.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every conversion runs in its own disposable profile, with no cache and nothing shared between
    /// calls. While it runs, the profile holds an owner lock, so the orphan sweep cannot delete it out
    /// from under the browser. Free disk space is checked up front, because a full disk makes Chromium
    /// hang rather than fail.
    /// </para>
    /// <para>
    /// The <see cref="CancellationToken"/> is honoured throughout, including while waiting for another
    /// conversion to finish. Past the conversion timeout there is a hard deadline that kills the
    /// browser when even cancellation goes unobserved.
    /// </para>
    /// <para>
    /// The instance is safe for concurrent use and expensive to create: use one. Under dependency
    /// injection the container disposes it; created by hand, call <see cref="Dispose"/> when done.
    /// </para>
    /// <para>
    /// Settings are copied at construction, so changing the object afterwards has no effect. To run
    /// with different settings, build another converter.
    /// </para>
    /// </remarks>
    public sealed class ChromiumHtmlToPdfConverter : IPdfConverter, IDisposable
    {
        private const string ProfilesDirectoryName = "boletoforbanrisul-chromium-profiles";

        /// <summary>
        /// File that marks a profile as belonging to a conversion in progress.
        /// </summary>
        internal const string OwnerLockFileName = ".owner.lock";

        // The pid is in the profile name for diagnostics only - knowing whose directory was left
        // behind. Never as a sign of life: the OS recycles pids, and in a container the same number
        // exists in different namespaces that may share the mounted TEMP.
        private static readonly string ProfileNamePrefix =
            "p" + GetCurrentProcessId().ToString(CultureInfo.InvariantCulture) + "-";

        // Process.Kill(bool) is not part of netstandard2.0 but exists from .NET Core 3.0 onwards.
        // Resolved once per process; null on runtimes that lack it.
        private static readonly MethodInfo? KillEntireProcessTreeMethod =
            typeof(Process).GetMethod("Kill", new[] { typeof(bool) });

        // Waits between attempts at deleting the conversion's profile. See DeleteProfileDirectoryAsync.
        private static readonly TimeSpan[] ProfileDeleteRetryDelays =
        {
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(150),
            TimeSpan.FromMilliseconds(400)
        };

        // The sweep walks the whole temp directory, so it is shared by every instance: one sweep at a
        // time, per process.
        private static int _orphanCleanupRunning;

        // Clock of the last completed sweep; long.MinValue means "never swept". Stopwatch rather than
        // DateTime because the system clock can move backwards and jam the cleanup for good.
        private static long _lastOrphanCleanupTimestamp = long.MinValue;

        private readonly ChromiumConverterSettings _settings;
        private readonly SemaphoreSlim _conversionSemaphore;
        private readonly ILogger? _logger;

        // volatile: a conversion running on another thread has to see the disposal at its next
        // ThrowIfDisposed, and without the barrier the memory model does not guarantee that.
        private volatile bool _disposed;

        /// <summary>
        /// Creates the converter with the default settings.
        /// </summary>
        public ChromiumHtmlToPdfConverter() : this(new ChromiumConverterSettings())
        {
        }

        /// <summary>
        /// Creates the converter, applying a configuration action over the default settings.
        /// </summary>
        /// <param name="configureSettings">Action that adjusts the settings.</param>
        public ChromiumHtmlToPdfConverter(Action<ChromiumConverterSettings> configureSettings)
            : this(Configure(configureSettings))
        {
        }

        /// <summary>
        /// Creates the converter with the given settings.
        /// </summary>
        /// <param name="settings">Converter settings. Copied, so later changes to it have no effect.</param>
        public ChromiumHtmlToPdfConverter(ChromiumConverterSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // Copy first, then validate the copy: the object belongs to the caller and can change on
            // the next line. Validating what we do not keep would make every guarantee hold for an
            // instant - including the agreement between MaxConcurrentConversions and the semaphore's
            // capacity, fixed here and immutable from now on.
            _settings = settings.Clone();
            _settings.Validate();

            _logger = _settings.Logger;
            _conversionSemaphore = new SemaphoreSlim(_settings.MaxConcurrentConversions, _settings.MaxConcurrentConversions);
        }

        private static ChromiumConverterSettings Configure(Action<ChromiumConverterSettings> configureSettings)
        {
            if (configureSettings == null)
                throw new ArgumentNullException(nameof(configureSettings));

            var settings = new ChromiumConverterSettings();
            configureSettings(settings);

            return settings;
        }

        /// <summary>
        /// Root directory holding the temporary profiles the converter creates.
        /// </summary>
        public static string ProfilesRootDirectory => Path.Combine(Path.GetTempPath(), ProfilesDirectoryName);

        /// <summary>
        /// Directory name for a disposable profile, prefixed with the owner's pid.
        /// </summary>
        internal static string NewDisposableProfileName() => ProfileNamePrefix + Guid.NewGuid().ToString("N");

        // netstandard2.0 has no Environment.ProcessId, and Process.GetCurrentProcess() allocates a
        // handle per call. Read once, and never from a static initializer that could fail: a refused
        // query would surface as TypeInitializationException.
        private static int GetCurrentProcessId()
        {
            try
            {
                using var process = Process.GetCurrentProcess();

                return process.Id;
            }
            catch
            {
                return 0;
            }
        }

        /// <inheritdoc />
        /// <exception cref="InsufficientDiskSpaceException">Not enough free disk space to launch Chromium.</exception>
        /// <exception cref="TimeoutException">
        /// The conversion ran past its <see cref="ChromiumConverterSettings.ConversionTimeout"/> or the
        /// hard deadline, without the caller cancelling.
        /// </exception>
        /// <exception cref="PdfConversionException">The conversion failed for any other reason.</exception>
        public async Task<byte[]> ConvertToPdfAsync(string html, CancellationToken cancellationToken = default)
        {
            if (html == null)
                throw new ArgumentNullException(nameof(html));

            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            // Outside the semaphore: the cleanup does not compete for a conversion slot.
            ScheduleOrphanCleanupIfDue();

            await _conversionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                ThrowIfDisposed();

                EnsureFreeDiskSpace();

                var pdf = await ConvertAsync(html, cancellationToken).ConfigureAwait(false);

                // A conversion that "succeeded" without writing anything is a silent failure: the
                // caller files a zero byte PDF and only finds out when someone tries to open it. Kept
                // outside ConvertAsync so the catch ladder does not recapture and rewrap it.
                if (pdf.Length == 0)
                    throw new PdfConversionException("A conversão terminou sem produzir bytes de PDF.");

                return pdf;
            }
            finally
            {
                _conversionSemaphore.Release();
            }
        }

        private async Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken)
        {
            var isDisposableProfile = string.IsNullOrEmpty(_settings.UserProfileDirectory);

            string? profileDirectory = null;
            FileStream? ownerLock = null;
            ConverterHandle? handle = null;

            // Both sources are declared outside the try because the catch ladder needs them to tell
            // "the caller cancelled" from "time ran out" - and, between the two clocks, which one.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);

            try
            {
                profileDirectory = isDisposableProfile
                    ? Path.Combine(ProfilesRootDirectory, NewDisposableProfileName())
                    : _settings.UserProfileDirectory!;

                // Creating the directory, taking the lock and starting the Converter all belong inside
                // the try: any of them can fail - full disk, permissions, missing executable - and the
                // failure has to become a PdfConversionException and go through the finally that
                // deletes the profile.
                Directory.CreateDirectory(profileDirectory);

                // Disposable profiles only: the caller's directory is not under the swept root and is
                // never deleted.
                if (isDisposableProfile)
                    ownerLock = TryAcquireOwnerLock(profileDirectory);

                // The hard deadline sits past the conversion timeout, so Chromium gets a chance to
                // abort on its own - with logging and cleanup - before being killed.
                deadline.CancelAfter(_settings.HardDeadline);

                // The budget is armed here rather than left to the library, for two reasons. Its
                // timeout is not continuous: it covers the DevTools handshake and then only the
                // printing, leaving navigation uncovered. And it swallows its own cancellation - the
                // wait for a DevTools reply returns an empty string when the token fires, which
                // surfaces as a ConversionException indistinguishable from a content error. With the
                // token in our hands, knowing that time ran out does not depend on guessing from an
                // exception type.
                budget.CancelAfter(_settings.ConversionTimeout);

                handle = new ConverterHandle(
                    new Converter(
                        chromiumExeFileName: _settings.ChromiumExecutablePath,
                        userProfile: profileDirectory,
                        logger: _logger,
                        useCache: _settings.UseCache,
                        browser: _settings.Browser),
                    _logger);

                var converter = handle.Converter;

                converter.TempDirectory = profileDirectory;

                foreach (var argument in _settings.ChromiumArguments)
                {
                    converter.AddChromiumArgument(argument);
                }

                using var byteStream = new MemoryStream();

                var conversion = converter.ConvertToPdfAsync(
                    html,
                    byteStream,
                    _settings.PageSettings,
                    waitForWindowStatus: _settings.WaitForWindowStatus,
                    waitForWindowsStatusTimeout: _settings.WaitForWindowStatusTimeout,
                    conversionTimeout: _settings.ConversionTimeoutMilliseconds,
                    mediaLoadTimeout: _settings.MediaLoadTimeout,
                    logger: _logger,
                    cancellationToken: budget.Token);

                await AwaitWithHardDeadline(conversion, handle, deadline.Token).ConfigureAwait(false);

                return byteStream.ToArray();
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                // The library observes the linked token, so the exception carries the deadline's token
                // rather than the caller's - leaving the caller unable to recognise its own
                // cancellation in OperationCanceledException.CancellationToken. Rethrowing as-is is the
                // only way to keep the original stack, so it is used when the two already match.
                if (exception.CancellationToken == cancellationToken)
                    throw;

                throw new OperationCanceledException("A conversão para PDF foi cancelada.", exception, cancellationToken);
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested)
            {
                // Cancelled mid-startup, the library reports the interruption with whatever is at hand
                // - a NullReferenceException from an aborted DevTools handshake, a ConversionException,
                // a TaskCanceledException - depending on exactly where the token caught it. Whoever
                // cancelled gets cancellation; what the library threw stays in the log and the
                // InnerException.
                _logger?.LogWarning(exception,
                    "A conversão cancelada veio à tona como {exceptionType}; tratada como cancelamento.",
                    exception.GetType().Name);

                throw new OperationCanceledException("A conversão para PDF foi cancelada.", exception, cancellationToken);
            }
            catch (TimeoutException)
            {
                // Already translated by AwaitWithHardDeadline, the only place that knows the browser
                // was force-killed. Rewrapping would trade that message for a vaguer one - and, if the
                // deadline token is not cancelled, it would slip down to the generic clause and come
                // out as PdfConversionException, which the caller reads as retryable.
                throw;
            }
            catch (Exception exception) when (deadline.IsCancellationRequested)
            {
                // Past the deadline Chromium has already been killed, so whatever the library throws
                // from here on is a consequence of the forced stop, not its cause.
                throw new TimeoutException(
                    $"A conversão para PDF não terminou dentro de {_settings.ConversionTimeout.TotalSeconds:0.#}s (+{_settings.HardTimeoutGrace.TotalSeconds:0.#}s de folga) e foi abortada.",
                    exception);
            }
            catch (Exception exception) when (budget.IsCancellationRequested || IsLibraryConversionTimeout(exception))
            {
                // The hard deadline has not arrived, but the conversion budget has. Without this step a
                // real timeout would come out as PdfConversionException and the caller would retry
                // against a wedged browser. The token decides; the exception type is only the fallback
                // for the library's own timeout winning the race.
                throw new TimeoutException(
                    $"A conversão para PDF estourou o tempo de {_settings.ConversionTimeout.TotalSeconds:0.#}s.",
                    exception);
            }
            catch (Exception exception)
            {
                throw new PdfConversionException("Falha ao converter o HTML do boleto em PDF.", exception);
            }
            finally
            {
                // This disposal is not hygiene, it is the only thing that collects the process on some
                // paths: when the library gives up on the DevTools handshake it throws and leaves
                // Chromium running - what kills it is the KillProcessAndChildren inside here.
                // The order matters: killing Chromium is part of the conversion, so the lock is only
                // released afterwards - and it has to be released before the delete, because on Windows
                // an open handle blocks removing the file and leaves the directory half deleted.
                handle?.Dispose();

                // Read after the disposal: the handle captures the identity at the start of its own
                // Dispose, the last moment the process still exists. Reading it earlier would return
                // null on the hard deadline path, where the disposal already happened further back.
                var browser = handle?.Identity;

                LastObservedBrowserProcess = browser;

                EnsureBrowserIsGone(browser);
                ReleaseOwnerLock(ownerLock);

                if (isDisposableProfile && profileDirectory != null)
                    await DeleteProfileDirectoryAsync(profileDirectory).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Identity of the last browser process seen while a conversion was being torn down.
        /// </summary>
        /// <remarks>
        /// Diagnostics, and meant to be read as such: with <c>MaxConcurrentConversions</c> above one it
        /// is "the last one seen", not "this call's". It lets tests assert the process really ended
        /// instead of inferring it from the directory being gone - which proves nothing on Linux, where
        /// <c>unlink</c> removes the entry even with descriptors still open.
        /// </remarks>
        internal BrowserProcessIdentity? LastObservedBrowserProcess { get; private set; }

        /// <summary>
        /// Kills the browser process when it survived the library's disposal.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Covers one case, and only one: a main process still alive. The library calls
        /// <c>Process.Kill()</c> on it and nothing else - despite being named
        /// <c>KillProcessAndChildren</c>, it does not use the overload that takes down the tree. When
        /// that <c>Kill</c> has no effect, this is where the process falls, and the tree goes with it
        /// because the parent still exists.
        /// </para>
        /// <para>
        /// It does not cover children that outlive a dead parent. Once the parent's pid is gone there
        /// is nobody to call <c>Kill(entireProcessTree)</c> on, and an orphaned renderer is out of
        /// reach. Chromium usually takes its children along, but usually is an observation, not a
        /// guarantee: in a container a surviving renderer is memory nobody reclaims until the pod dies.
        /// </para>
        /// <para>
        /// <c>Kill(bool)</c> is absent from netstandard2.0 but present from .NET Core 3.0 onwards, so
        /// it is resolved by reflection. On older runtimes, .NET Framework included, only the plain
        /// <c>Kill()</c> is available and the tree is out of reach even with the parent alive.
        /// </para>
        /// <para>
        /// Never throws: it runs inside the conversion's <c>finally</c>, where an exception would
        /// replace the real one and trade the caller's diagnosis for a cleanup failure.
        /// </para>
        /// <para>
        /// It is a safety net, and the integration suite stays green with it disabled, because on the
        /// paths that suite covers the library's disposal already kills the main process. Anyone
        /// removing this as dead code should know the tests will not object.
        /// </para>
        /// </remarks>
        private void EnsureBrowserIsGone(BrowserProcessIdentity? browser)
        {
            if (browser == null)
                return;

            try
            {
                using var process = Process.GetProcessById(browser.Value.Id);

                // Recycled number: this is somebody else's process, and killing it would be killing an
                // innocent.
                if (process.StartTime != browser.Value.StartTime)
                    return;

                if (process.HasExited)
                    return;

                _logger?.LogWarning(
                    "O Chromium (ProcessId={processId}) sobreviveu ao encerramento da conversão; derrubando a árvore de processos.",
                    browser.Value.Id);

                KillProcessTree(process);

                // Short on purpose: the point is to record the outcome, not to hold up the caller.
                if (!process.WaitForExit(2000))
                    _logger?.LogError("O Chromium (ProcessId={processId}) não terminou depois de derrubado.", browser.Value.Id);
            }
            catch (ArgumentException)
            {
                // No process with that number: it ended, which is the expected outcome.
            }
            catch (InvalidOperationException)
            {
                // It ended between the lookup and the query.
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "Não foi possível confirmar o encerramento do Chromium da conversão.");
            }
        }

        private static void KillProcessTree(Process process)
        {
            if (KillEntireProcessTreeMethod != null)
                KillEntireProcessTreeMethod.Invoke(process, new object[] { true });
            else
                process.Kill();
        }

        /// <summary>
        /// Tells whether the exception is the library reporting its own conversion timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A fallback, not the main criterion. What decides that time ran out is the token the
        /// converter arms: a cancellation caused by the budget implies that token is already cancelled
        /// by the time the <c>catch</c> runs. This predicate is for the library's internal timeout
        /// winning the race, which it rarely does - it is armed only after navigation and lasts as long
        /// as ours, which starts counting earlier.
        /// </para>
        /// <para>
        /// Cancellation types must not be added here. The budget never reports itself that way, and an
        /// unrelated cancellation - the <c>HttpClient</c> timeout the library raises while loading
        /// images surfaces as <see cref="TaskCanceledException"/> - would be read as a spent budget,
        /// telling the caller to wait out a wedged browser over a picture that failed to download.
        /// </para>
        /// <para>
        /// The same reasoning keeps <c>TaskTimedOutException</c> out: it is the deadline of a single
        /// DevTools message, which can expire three seconds into a two minute budget. Classification is
        /// by <em>spent budget</em>, not by the word "timeout" in a type name.
        /// </para>
        /// <para>
        /// The line between a retryable startup failure and a timeout is the clock, not this predicate.
        /// A startup that fails outright - missing executable, invalid argument - arrives before the
        /// budget expires and comes out as <see cref="PdfConversionException"/>: the browser is
        /// unreachable, not wedged, and retrying now is the right move. A startup that <em>stalls</em>
        /// consumes the whole budget and comes out as <see cref="TimeoutException"/>. Either way the
        /// process was already killed by the conversion's <c>finally</c>, so a retry starts clean.
        /// </para>
        /// </remarks>
        internal static bool IsLibraryConversionTimeout(Exception exception)
            => exception is ConversionTimedOutException;

        /// <summary>
        /// Awaits the conversion within the deadline, killing Chromium if it stops responding.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Cancellation is cooperative: the token only marks the state as cancelled, somebody has to
        /// observe it. If Chromium hangs at a level that never returns the <c>await</c> - a mute
        /// devtools websocket, a zombie child process - nobody does, and the <see cref="Task"/> never
        /// completes. Disposing the <c>Converter</c> kills the process and makes the stuck conversion
        /// fail, which is the only remedy there.
        /// </para>
        /// <para>
        /// The <see cref="TimeoutException"/> raised here travels intact: the conversion's catch ladder
        /// has a clause that rethrows it unwrapped, because this is the only place that knows the
        /// browser had to be killed, and that is what the caller needs to read.
        /// </para>
        /// <para>
        /// This branch has no automated coverage. Exercising it takes a conversion that never
        /// completes, which on demand would require an internal seam for faking the browser session -
        /// deliberately not introduced. Anyone touching it has to check the four effects by hand: the
        /// <c>Converter</c> disposed, the abandoned task observed, the profile removed while that task
        /// is still finishing, and <see cref="TimeoutException"/> reaching the caller. It is the
        /// riskiest path in the file - orphaned process, disposed stream, stuck profile - and the least
        /// exercised.
        /// </para>
        /// </remarks>
        private async Task AwaitWithHardDeadline(Task conversion, ConverterHandle handle, CancellationToken deadlineToken)
        {
            // Its own source, cancelled in the finally, so the wait ends together with the conversion
            // instead of lingering.
            using var deadlineWait = CancellationTokenSource.CreateLinkedTokenSource(deadlineToken);

            try
            {
                var abandoned = Task.Delay(Timeout.Infinite, deadlineWait.Token);

                if (ReferenceEquals(await Task.WhenAny(conversion, abandoned).ConfigureAwait(false), conversion))
                {
                    await conversion.ConfigureAwait(false);
                    return;
                }

                _logger?.LogError(
                    "A conversão para PDF não respondeu ao cancelamento; derrubando o Chromium (ProcessId={processId}).",
                    handle.SafeProcessId);

                handle.Dispose();
                ObserveInBackground(conversion);

                // Unconditional, rather than ThrowIfCancellationRequested: reaching this point means
                // Chromium was just killed and the caller's byteStream holds half a PDF at best. If the
                // token somehow is not cancelled, returning those bytes is worse than failing - a
                // truncated PDF passes for a valid one until the customer opens it.
                // Thrown already translated, instead of an OperationCanceledException for the ladder to
                // interpret: this is the only place that knows the browser stopped responding and had
                // to be killed, and that would be lost in a translation by type further up.
                throw new TimeoutException(
                    $"A conversão para PDF não respondeu ao cancelamento dentro de {_settings.ConversionTimeout.TotalSeconds:0.#}s (+{_settings.HardTimeoutGrace.TotalSeconds:0.#}s de folga); o Chromium foi derrubado.");
            }
            finally
            {
                deadlineWait.Cancel();
            }
        }

        /// <summary>
        /// Opens the lock that marks the profile as belonging to this conversion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It lives inside the profile rather than beside it, so directory and lock share one lifetime:
        /// deleting the profile deletes the lock, and no second kind of litter appears - the
        /// <c>.lock</c> file whose directory is long gone - for somebody to sweep later. Chromium
        /// ignores files it does not recognise at the root of <c>--user-data-dir</c>, and the directory
        /// is not a pristine profile anyway: it doubles as the conversion's <c>TempDirectory</c>.
        /// </para>
        /// <para>
        /// <see cref="FileShare.None"/> is what provides cross-process exclusion: on Windows the
        /// sharing mode is enforced by the kernel, on Linux <see cref="FileStream"/> translates it to
        /// <c>flock(LOCK_EX)</c>. On both the handle returns to the OS when the owner dies, SIGKILL
        /// included - there is no stale lock to recover from.
        /// </para>
        /// <para>
        /// No <c>DeleteOnClose</c>, on purpose: the leftover file is what tells the sweep this profile
        /// is lock-managed. Failing here does not fail the conversion - without a lock the profile
        /// falls back to the age criterion alone.
        /// </para>
        /// </remarks>
        private FileStream? TryAcquireOwnerLock(string profileDirectory)
        {
            var lockPath = Path.Combine(profileDirectory, OwnerLockFileName);

            try
            {
                return new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, bufferSize: 1);
            }
            catch (Exception exception)
            {
                _logger?.LogDebug(exception,
                    "Não foi possível criar o lock do profile {path}; a limpeza de órfãos vai depender só da idade.", lockPath);

                return null;
            }
        }

        /// <summary>
        /// Releases the profile lock without letting a close failure override the real exception.
        /// </summary>
        private void ReleaseOwnerLock(FileStream? ownerLock)
        {
            if (ownerLock == null)
                return;

            try
            {
                ownerLock.Dispose();
            }
            catch (Exception exception)
            {
                // An exception thrown from inside the finally would replace the conversion's real one;
                // closing an empty file is no reason to lose the diagnosis.
                _logger?.LogDebug(exception, "Falha ao liberar o lock do profile da conversão.");
            }
        }

        /// <summary>
        /// Tells whether the profile still belongs to a live conversion, by the lock its owner holds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The lock is a veto, not a trigger: no lock file is no proof of abandonment - a profile can
        /// be lock-less, and there is a window of microseconds between creating the directory and
        /// opening the file - so what decides which profiles to delete is still the age. All that is
        /// decided here is which ones <em>not</em> to delete.
        /// </para>
        /// <para>
        /// Never delete-and-see: on Windows a recursive <c>Directory.Delete</c> erases everything it
        /// can before failing on the locked file, and on Linux <c>unlink</c> ignores <c>flock</c> and
        /// takes the whole profile out from under a running Chromium.
        /// </para>
        /// </remarks>
        private static bool IsProfileInUse(string profileDirectory, ILogger? logger)
        {
            var lockPath = Path.Combine(profileDirectory, OwnerLockFileName);

            try
            {
                // FileShare.None on both sides: on Linux only that maps to LOCK_EX. A FileShare.Read
                // here would take LOCK_SH, would not conflict with the owner, and the probe would lie -
                // on Linux only.
                using var probe = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize: 1);

                return false;
            }
            // FileNotFoundException and DirectoryNotFoundException derive from IOException: reversed,
            // this order would read "no lock" as "in use" and nothing would ever be collected again.
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                // Sharing violation: somebody has the file open, and the only one who opens it is the
                // owner.
                return true;
            }
            catch (UnauthorizedAccessException exception)
            {
                // Another user's profile, or permission denied: not ours to delete.
                logger?.LogDebug(exception, "Sem acesso ao lock do profile {path}; o diretório será preservado.", lockPath);

                return true;
            }
        }

        /// <summary>
        /// Fails fast when the disk is full, instead of letting Chromium hang mid-conversion.
        /// </summary>
        private void EnsureFreeDiskSpace()
        {
            if (_settings.MinimumFreeDiskMegabytes <= 0)
                return;

            var path = string.IsNullOrEmpty(_settings.UserProfileDirectory)
                ? ProfilesRootDirectory
                : _settings.UserProfileDirectory!;

            long freeBytes;

            try
            {
                freeBytes = GetAvailableFreeSpace(path);
            }
            catch (Exception exception)
            {
                // The check is extra protection, not a prerequisite: failing to measure must not refuse
                // the conversion.
                _logger?.LogDebug(exception, "Não foi possível verificar o espaço livre em disco para {path}.", path);
                return;
            }

            var minimumFreeBytes = _settings.MinimumFreeDiskMegabytes * 1024L * 1024L;

            if (freeBytes < minimumFreeBytes * 2)
            {
                _logger?.LogWarning(
                    "Pouco espaço livre em disco para {path}: {freeBytes} bytes livres (limite de aviso {warnBytes} bytes).",
                    path, freeBytes, minimumFreeBytes * 2);
            }

            if (freeBytes < minimumFreeBytes)
            {
                _logger?.LogError(
                    "Espaço em disco insuficiente para iniciar o Chromium em {path}: {freeBytes} bytes livres, mínimo de {requiredBytes} bytes.",
                    path, freeBytes, minimumFreeBytes);

                throw new InsufficientDiskSpaceException(path, freeBytes, minimumFreeBytes);
            }
        }

        private static long GetAvailableFreeSpace(string path)
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));

            // On Linux the root of any absolute path is "/", which is always a valid drive.
            return new DriveInfo(string.IsNullOrEmpty(root) ? "/" : root).AvailableFreeSpace;
        }

        /// <summary>
        /// Deletes temporary profiles left behind by processes that died mid-conversion.
        /// </summary>
        /// <param name="olderThan">Minimum age for a profile to count as an orphan. Negative values are treated as zero.</param>
        /// <param name="logger">Optional logger for cleanup failures.</param>
        /// <remarks>
        /// <para>
        /// Age is the trigger, the lock is the veto. Only profiles idle for longer than
        /// <paramref name="olderThan"/> are considered - the temp directory can be shared by more than
        /// one process, and deleting the whole root would take down a neighbour's running conversion -
        /// and before being deleted each candidate still has to prove it has no live owner.
        /// </para>
        /// <para>
        /// Neither criterion is enough alone: the root directory's timestamp is no proof of inactivity,
        /// since Chromium writes deep in the tree, and the lock does not cover lock-less profiles nor
        /// the instant between creating the directory and opening the file.
        /// </para>
        /// </remarks>
        public static void CleanupOrphanedProfiles(TimeSpan olderThan, ILogger? logger = null)
        {
            var profilesRoot = ProfilesRootDirectory;

            if (!Directory.Exists(profilesRoot))
                return;

            // A negative age pushes the cutoff into the future. In practice that matches zero - a
            // legitimate argument that makes every profile a candidate - so the effect is not undue
            // deletion: what protects a live conversion is the lock, never the age. It is meaningless
            // input, and normalising it with a warning lets the log say what happened instead of
            // leaving the caller's intent to guesswork. A warning and not an exception, because the
            // method is public and refusing the whole cleanup would be worse than running it.
            if (olderThan < TimeSpan.Zero)
            {
                logger?.LogWarning(
                    "Idade mínima negativa ({olderThan}) na limpeza de profiles órfãos; tratada como zero.", olderThan);

                olderThan = TimeSpan.Zero;
            }

            var cutoff = DateTime.UtcNow - olderThan;

            List<string> profiles;

            try
            {
                // Materialised inside the try: EnumerateDirectories is lazy, so the actual read would
                // happen in the foreach - outside this try, letting an I/O failure escape the method
                // instead of becoming a warning.
                profiles = new List<string>(Directory.EnumerateDirectories(profilesRoot));
            }
            catch (Exception exception)
            {
                logger?.LogWarning(exception, "Não foi possível listar os profiles do Chromium em {directory}.", profilesRoot);
                return;
            }

            foreach (var profile in profiles)
            {
                try
                {
                    // Cheap coarse filter first: a young profile almost always has an owner, and it is
                    // not worth opening a file to find out.
                    if (Directory.GetLastWriteTimeUtc(profile) >= cutoff)
                        continue;

                    // Fine filter: the parent directory's timestamp is no proof of inactivity, and the
                    // root is shared with processes that may have a longer deadline than ours, about
                    // which this sweep knows nothing.
                    if (IsProfileInUse(profile, logger))
                    {
                        logger?.LogDebug("O profile {directory} ainda tem dono; a remoção fica para a próxima varredura.", profile);
                        continue;
                    }

                    Directory.Delete(profile, recursive: true);

                    logger?.LogInformation("Profile órfão do Chromium removido: {directory}.", profile);
                }
                catch (Exception exception)
                {
                    logger?.LogWarning(exception, "Não foi possível remover o profile órfão do Chromium {directory}.", profile);
                }
            }
        }

        /// <summary>
        /// Starts the orphan sweep when it is due.
        /// </summary>
        /// <remarks>
        /// Outside the semaphore and in the background: enumerating and deleting directories is I/O
        /// with no business inside the response time of whoever asked for a PDF. The trigger is a
        /// conversion rather than a timer, because an orphaned profile is only ever born of a
        /// conversion - an idle process has nothing to clean.
        /// </remarks>
        private void ScheduleOrphanCleanupIfDue()
        {
            if (!IsOrphanCleanupDue(_settings.OrphanCleanupInterval))
                return;

            // Two simultaneous sweeps would fight over the same directories and yield nothing but
            // failed-removal warnings.
            if (Interlocked.CompareExchange(ref _orphanCleanupRunning, 1, 0) != 0)
                return;

            var maxAge = EffectiveOrphanProfileMaxAge;
            var logger = _logger;

            // A dedicated thread rather than the pool: the sweep is blocking I/O of unknown duration,
            // and the pool is the same one carrying the conversion's continuations. One thread an hour
            // is far too cheap to be worth competing with somebody waiting for a PDF.
            _ = Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        CleanupOrphanedProfiles(maxAge, logger);
                    }
                    catch (Exception exception)
                    {
                        // Nobody observes this Task: an uncaught exception here surfaces as
                        // UnobservedTaskException and can bring the host down.
                        logger?.LogWarning(exception, "Falha na limpeza de profiles órfãos do Chromium.");
                    }
                    finally
                    {
                        // Stamped at the end, not the start: a slow sweep should not come due on its
                        // own.
                        Interlocked.Exchange(ref _lastOrphanCleanupTimestamp, Stopwatch.GetTimestamp());
                        Interlocked.Exchange(ref _orphanCleanupRunning, 0);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        private static bool IsOrphanCleanupDue(TimeSpan interval)
        {
            var last = Interlocked.Read(ref _lastOrphanCleanupTimestamp);

            if (last == long.MinValue)
                return true;

            if (interval == Timeout.InfiniteTimeSpan)
                return false;

            var elapsed = (Stopwatch.GetTimestamp() - last) / (double)Stopwatch.Frequency;

            return elapsed >= interval.TotalSeconds;
        }

        /// <summary>
        /// Effective minimum age for treating a profile as an orphan: never below the conversion
        /// deadline.
        /// </summary>
        /// <remarks>
        /// The sweep runs alongside conversions, so a short
        /// <see cref="ChromiumConverterSettings.OrphanProfileMaxAge"/> taken literally would delete the
        /// <c>--user-data-dir</c> of a conversion in progress. A profile younger than
        /// <c>ConversionTimeout + HardTimeoutGrace</c> may still have an owner.
        /// The floor only matters for profiles <em>without</em> an owner lock. Everywhere else the lock
        /// is what protects a live conversion, and unlike this floor it also covers a neighbouring
        /// process, whose settings this instance knows nothing about.
        /// </remarks>
        private TimeSpan EffectiveOrphanProfileMaxAge
        {
            get
            {
                var deadline = _settings.ConversionTimeout + _settings.HardTimeoutGrace;

                return _settings.OrphanProfileMaxAge > deadline ? _settings.OrphanProfileMaxAge : deadline;
            }
        }

        /// <summary>
        /// Removes the conversion's disposable profile, with short retries.
        /// </summary>
        /// <remarks>
        /// Killing Chromium does not return the handles right away: Crashpad keeps
        /// <c>CrashpadMetrics-active.pma</c> - 1 MB, memory mapped - held for a moment after the
        /// process dies, and the removal fails with access denied on exactly that file, after having
        /// already deleted the rest of the profile. The wait is short - in practice the handle is gone
        /// by the first retry - but without it 1 MB is left behind per conversion.
        /// </remarks>
        private async Task DeleteProfileDirectoryAsync(string directory)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, recursive: true);

                    return;
                }
                catch (Exception exception) when (attempt < ProfileDeleteRetryDelays.Length &&
                                                  (exception is IOException || exception is UnauthorizedAccessException))
                {
                    // No token: cancelling the conversion is no reason to leave the profile behind.
                    await Task.Delay(ProfileDeleteRetryDelays[attempt]).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    // The profile is left to the orphan cleanup; losing the cleanup must not cost the
                    // PDF that was already produced.
                    _logger?.LogWarning(exception, "Não foi possível remover o profile do Chromium {directory}.", directory);
                    return;
                }
            }
        }

        private void ObserveInBackground(Task task)
        {
            // The abandoned conversion will still fail once the process dies; without observing the
            // exception it surfaces as UnobservedTaskException and can bring the host down. Logging at
            // Debug keeps what the library actually threw, which here is expected rather than an error.
            _ = task.ContinueWith(
                completed => _logger?.LogDebug(completed.Exception, "A conversão abandonada falhou depois de o Chromium ser derrubado."),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChromiumHtmlToPdfConverter));
        }

        /// <summary>
        /// Releases the converter's resources.
        /// </summary>
        /// <remarks>
        /// The <see cref="SemaphoreSlim"/> is deliberately not disposed. The only resource it can hold
        /// is <c>AvailableWaitHandle</c>, created on demand and never touched by this class, so
        /// disposing it releases nothing - but it makes the <c>Release()</c> in an in-flight
        /// conversion's <c>finally</c> throw <see cref="ObjectDisposedException"/>, swallowing the
        /// finished PDF or, worse, the exception that explained the failure. What blocks use after
        /// disposal is <c>ThrowIfDisposed</c>, not the semaphore's state.
        /// </remarks>
        public void Dispose()
        {
            _disposed = true;
        }

        /// <summary>
        /// Identifies a conversion's browser process unambiguously.
        /// </summary>
        /// <remarks>
        /// A struct rather than a named tuple so the meaning of the pair lives in the type: a process
        /// number alone identifies nothing stable, because the system recycles it.
        /// </remarks>
        internal readonly struct BrowserProcessIdentity
        {
            internal BrowserProcessIdentity(int id, DateTime startTime)
            {
                Id = id;
                StartTime = startTime;
            }

            internal int Id { get; }

            internal DateTime StartTime { get; }
        }

        /// <summary>
        /// Wraps the <see cref="Converter"/> so disposal is idempotent: it is called both on the
        /// deadline path - where killing Chromium is the action - and in the <c>finally</c>.
        /// </summary>
        /// <remarks>
        /// Disposal blocks and can cost up to two seconds: the library gives Chromium that long to
        /// close on its own before killing the process, and on the netstandard2.0 branch that wait uses
        /// <c>Thread.Sleep</c>. It is the price of not leaving a process behind, and the time floor of
        /// any conversion that ends in cancellation or failure.
        /// </remarks>
        private sealed class ConverterHandle : IDisposable
        {
            private readonly ILogger? _logger;
            private BrowserProcessIdentity? _identity;
            private int _disposed;

            internal ConverterHandle(Converter converter, ILogger? logger)
            {
                Converter = converter;
                _logger = logger;
            }

            internal Converter Converter { get; }

            internal int? SafeProcessId
            {
                get
                {
                    try
                    {
                        return Converter.ChromiumProcessId;
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// Identity of the browser process, captured while it still existed.
            /// </summary>
            /// <remarks>
            /// Held in a field rather than queried on demand, because the library's disposal forgets
            /// which process it was - and on the hard deadline path that disposal happens well before
            /// the <c>finally</c> that needs this.
            /// </remarks>
            internal BrowserProcessIdentity? Identity => _identity;

            /// <summary>
            /// Records the browser process identity, if it has started and has not been read yet.
            /// </summary>
            /// <remarks>
            /// The pid paired with the start time, not the pid alone, because the system recycles
            /// process numbers: checking the start time later is what prevents killing somebody else's
            /// process that inherited the number.
            /// </remarks>
            internal void CaptureIdentity()
            {
                if (_identity != null)
                    return;

                var processId = SafeProcessId;

                if (processId == null || processId.Value == 0)
                    return;

                try
                {
                    using var process = Process.GetProcessById(processId.Value);

                    _identity = new BrowserProcessIdentity(process.Id, process.StartTime);
                }
                catch
                {
                    // Already dead, or the system refused the query: without a start time there is no
                    // identity safe to check later, and acting on the pid alone is worse than not
                    // acting.
                }
            }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                    return;

                // Always before disposing: the last chance to know which process it was.
                CaptureIdentity();

                try
                {
                    Converter.Dispose();
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception, "Falha ao encerrar o Chromium após a conversão.");
                }
            }
        }
    }
}
