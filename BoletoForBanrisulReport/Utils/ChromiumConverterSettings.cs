using ChromiumHtmlToPdfLib.Enums;
using ChromiumHtmlToPdfLib.Settings;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace BoletoForBanrisul.Report.Utils
{
    /// <summary>
    /// Settings for <see cref="ChromiumHtmlToPdfConverter"/>.
    /// </summary>
    /// <remarks>
    /// The defaults are deliberately conservative: a disposable profile per conversion, no cache, a
    /// short timeout and a disk pre-check. The converter copies these settings at construction, so
    /// changing the object afterwards has no effect on a converter already built.
    /// </remarks>
    public class ChromiumConverterSettings
    {
        // The timeout becomes an int of milliseconds in the library call, and the hard deadline becomes
        // a CancelAfter: both overflow above int.MaxValue ms (~24.8 days). The ceiling is validated in
        // parts so that not even the sum itself can overflow the TimeSpan.
        private static readonly TimeSpan MaximumDeadline = TimeSpan.FromMilliseconds(int.MaxValue);

        // The library refuses conversionTimeout <= 1 ms, but the useful floor is elsewhere: below one
        // second the value can only produce failure, because Chromium spends that same budget starting
        // up and completing the DevTools handshake.
        private static readonly TimeSpan MinimumConversionTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Arguments passed to Chromium. Comes pre-filled with the defaults; add or remove items
        /// rather than replacing the list.
        /// </summary>
        /// <remarks>
        /// The first five are what headless needs in a container; the rest silence network, GCM and
        /// component-update noise, typical of a Linux box with no graphical session.
        /// </remarks>
        public IList<string> ChromiumArguments { get; } = new List<string>
        {
            "--disable-dev-shm-usage",
            "--disable-gpu",
            "--disable-software-rasterizer",
            "--no-zygote",
            "--disable-crash-reporter",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-background-networking",
            "--disable-sync",
            "--disable-component-update",
            "--disable-extensions"
        };

        /// <summary>
        /// Time budget for a whole conversion. Default: 2 minutes.
        /// </summary>
        /// <remarks>
        /// Counted end to end by the converter itself, covering startup, navigation and printing. The
        /// value is also handed to the library, but the count that matters is the converter's: the
        /// library's timeout bounds the DevTools handshake and then only the printing, leaving
        /// navigation uncovered.
        /// </remarks>
        public TimeSpan ConversionTimeout { get; set; } = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Extra grace after <see cref="ConversionTimeout"/> before the converter force-kills Chromium.
        /// Default: 30 seconds.
        /// </summary>
        /// <remarks>
        /// A safety net for when Chromium hangs at a level that never returns the <c>await</c>. There
        /// <see cref="ConversionTimeout"/> has no effect, because nobody is observing the cancellation,
        /// and only killing the process helps.
        /// </remarks>
        public TimeSpan HardTimeoutGrace { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How many times to retry when Chromium fails to start. Default: 2. <c>0</c> turns retrying off.
        /// </summary>
        /// <remarks>
        /// Covers one class of failure: a browser that dies while starting up. It is genuinely
        /// transient, because both the process and the profile are new on every attempt. It does not
        /// cover a conversion that <em>stalls</em> - that consumes the whole
        /// <see cref="ConversionTimeout"/> and comes out as <see cref="TimeoutException"/>, which is
        /// never retried here. Each attempt arms its own budget, so in the worst case the wall-clock
        /// time grows with the retry count; in practice a startup failure of this kind surfaces in
        /// under a second, so the added cost is mostly the price of another browser launch.
        /// </remarks>
        /// <seealso cref="ChromiumHtmlToPdfConverter.IsTransientBrowserStartupFailure(Exception)"/>
        public int BrowserStartupRetries { get; set; } = 2;

        /// <summary>
        /// Chromium profile directory (<c>--user-data-dir</c>). Null or empty (the default) gives each
        /// conversion its own profile under a temporary directory, deleted when it ends.
        /// </summary>
        /// <remarks>
        /// Set it, and every conversion shares one profile: it grows without bound and, if corrupted by
        /// a Chromium that died mid-conversion, keeps breaking the conversions that follow. Use only
        /// with a concrete reason.
        /// </remarks>
        public string? UserProfileDirectory { get; set; }

        /// <summary>
        /// Enables Chromium's on-disk cache. Default: <c>false</c>.
        /// </summary>
        public bool UseCache { get; set; } = false;

        /// <summary>
        /// Minimum free space, in megabytes, required before launching Chromium. Default: 500. Use
        /// <c>0</c> to turn the check off.
        /// </summary>
        /// <remarks>
        /// Below the limit the conversion fails immediately with
        /// <see cref="Exceptions.InsufficientDiskSpaceException"/>. At twice the limit a warning is
        /// logged, to leave time to react before the disk actually runs out.
        /// </remarks>
        public int MinimumFreeDiskMegabytes { get; set; } = 500;

        /// <summary>
        /// Conversions allowed to run at the same time per converter instance. Default: 1.
        /// </summary>
        /// <remarks>
        /// Each concurrent conversion is one more Chromium process, with the matching cost in memory
        /// and disk. Raise it only after measuring.
        /// </remarks>
        public int MaxConcurrentConversions { get; set; } = 1;

        /// <summary>
        /// Path to the Chrome/Chromium executable. Null means locate it automatically.
        /// </summary>
        public string? ChromiumExecutablePath { get; set; }

        /// <summary>
        /// Browser to use. Default: <see cref="ChromiumHtmlToPdfLib.Enums.Browser.Chrome"/>.
        /// </summary>
        public Browser Browser { get; set; } = Browser.Chrome;

        /// <summary>
        /// Page settings (paper, margins, orientation) used when printing.
        /// </summary>
        public PageSettings PageSettings { get; set; } = new PageSettings();

        /// <summary>
        /// Value of <c>window.status</c> to wait for before printing. Null (the default) does not wait.
        /// </summary>
        public string? WaitForWindowStatus { get; set; }

        /// <summary>
        /// How long, in milliseconds, to wait for <see cref="WaitForWindowStatus"/>. Default: 60000.
        /// </summary>
        public int WaitForWindowStatusTimeout { get; set; } = 60000;

        /// <summary>
        /// How long, in milliseconds, to wait for media to load. Null (the default) uses the library's
        /// behaviour.
        /// </summary>
        public int? MediaLoadTimeout { get; set; }

        /// <summary>
        /// Optional logger, passed to the conversion library and used by the converter itself.
        /// </summary>
        /// <remarks>
        /// Without it, a stuck conversion is indistinguishable from a slow one. Worth configuring in
        /// any non-interactive environment.
        /// </remarks>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Age at which a forgotten temporary profile counts as an orphan and is deleted. Default: 1
        /// hour.
        /// </summary>
        /// <remarks>
        /// Age is the trigger, never the final authorisation: before deleting, the sweep confirms
        /// through the owner lock that the profile has no live owner, and it is that lock which
        /// protects a neighbouring process - including one configured with a longer deadline than this
        /// instance's. When sweeping on its own the converter still never uses a value below
        /// <see cref="ConversionTimeout"/> + <see cref="HardTimeoutGrace"/>, to cover profiles with no
        /// lock file.
        /// </remarks>
        /// <seealso cref="ChromiumHtmlToPdfConverter.CleanupOrphanedProfiles(TimeSpan, ILogger)"/>
        public TimeSpan OrphanProfileMaxAge { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Minimum interval between two orphan sweeps in the same process. Default: 1 hour. Use
        /// <see cref="Timeout.InfiniteTimeSpan"/> to sweep only once per process.
        /// </summary>
        /// <remarks>
        /// The sweep is triggered by a conversion rather than a timer: an orphaned profile is only ever
        /// born of a conversion, so an idle process accumulates nothing to clean.
        /// </remarks>
        public TimeSpan OrphanCleanupInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// <see cref="ConversionTimeout"/> in milliseconds, in the shape the library expects.
        /// </summary>
        /// <remarks>
        /// The cast to <see cref="int"/> is safe because <see cref="Validate"/> refuses any value above
        /// <c>int.MaxValue</c> milliseconds.
        /// </remarks>
        internal int ConversionTimeoutMilliseconds => (int)ConversionTimeout.TotalMilliseconds;

        /// <summary>
        /// The conversion's hard deadline: what kills Chromium when even cancellation goes unobserved.
        /// </summary>
        /// <remarks>
        /// It sits past <see cref="ConversionTimeout"/> because it covers something else. The budget
        /// cancels the conversion and counts on somebody observing the token; this sum is the time
        /// allowed for that observation to happen.
        /// </remarks>
        internal TimeSpan HardDeadline => ConversionTimeout + HardTimeoutGrace;

        /// <summary>
        /// An independent copy of these settings.
        /// </summary>
        /// <remarks>
        /// The converter freezes its settings at construction and never reads the caller's object
        /// again. Without that, everything <see cref="Validate"/> guarantees could be undone on the
        /// next line - and worse, <see cref="MaxConcurrentConversions"/> becomes the semaphore in the
        /// constructor and cannot change, so a later edit would let validation see a coherent
        /// configuration while the real capacity was another, such as a shared profile serving two
        /// simultaneous conversions.
        /// <see cref="PageSettings"/> is cloned by the library's own type, which implements
        /// <see cref="ICloneable"/>: copying it by hand here would leave out <c>PaperFormat</c>, whose
        /// setter is private, and would silently drop any property the library adds.
        /// <see cref="Logger"/> is carried by reference on purpose - it is a shared service, not a
        /// configuration value.
        /// </remarks>
        internal ChromiumConverterSettings Clone()
        {
            var clone = new ChromiumConverterSettings
            {
                ConversionTimeout = ConversionTimeout,
                HardTimeoutGrace = HardTimeoutGrace,
                BrowserStartupRetries = BrowserStartupRetries,
                UserProfileDirectory = UserProfileDirectory,
                UseCache = UseCache,
                MinimumFreeDiskMegabytes = MinimumFreeDiskMegabytes,
                MaxConcurrentConversions = MaxConcurrentConversions,
                ChromiumExecutablePath = ChromiumExecutablePath,
                Browser = Browser,

                // Left null when null: what complains is Validate, applied right after over this copy -
                // and it is the copy the validation has to hold for.
                PageSettings = PageSettings == null ? null! : (PageSettings)PageSettings.Clone(),

                WaitForWindowStatus = WaitForWindowStatus,
                WaitForWindowStatusTimeout = WaitForWindowStatusTimeout,
                MediaLoadTimeout = MediaLoadTimeout,
                Logger = Logger,
                OrphanProfileMaxAge = OrphanProfileMaxAge,
                OrphanCleanupInterval = OrphanCleanupInterval
            };

            // Read-only at the declaration, so it is filled in by hand rather than assigned.
            clone.ChromiumArguments.Clear();

            foreach (var argument in ChromiumArguments)
            {
                clone.ChromiumArguments.Add(argument);
            }

            return clone;
        }

        /// <summary>
        /// Validates the configured values. Called by the converter's constructor.
        /// </summary>
        internal void Validate()
        {
            if (ConversionTimeout < MinimumConversionTimeout)
                throw new ArgumentOutOfRangeException(nameof(ConversionTimeout), ConversionTimeout, $"O timeout de conversão deve ser de pelo menos {MinimumConversionTimeout.TotalSeconds:0.#}s.");

            if (HardTimeoutGrace < TimeSpan.Zero || HardTimeoutGrace > MaximumDeadline)
                throw new ArgumentOutOfRangeException(nameof(HardTimeoutGrace), HardTimeoutGrace, $"A folga do deadline deve ficar entre zero e {MaximumDeadline.TotalDays:0.#} dias.");

            // Subtraction rather than addition: with the grace already inside the range,
            // MaximumDeadline - HardTimeoutGrace cannot overflow - whereas ConversionTimeout +
            // HardTimeoutGrace would overflow the TimeSpan itself before any comparison got to run.
            if (ConversionTimeout > MaximumDeadline - HardTimeoutGrace)
                throw new ArgumentOutOfRangeException(nameof(ConversionTimeout), ConversionTimeout, $"ConversionTimeout + HardTimeoutGrace não pode passar de {MaximumDeadline.TotalDays:0.#} dias.");

            if (BrowserStartupRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(BrowserStartupRetries), BrowserStartupRetries, "O número de retentativas de subida do Chromium não pode ser negativo.");

            if (WaitForWindowStatusTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(WaitForWindowStatusTimeout), WaitForWindowStatusTimeout, "O timeout de espera por window.status não pode ser negativo.");

            if (MediaLoadTimeout.HasValue && MediaLoadTimeout.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(MediaLoadTimeout), MediaLoadTimeout, "O timeout de carregamento de mídia não pode ser negativo.");

            if (MinimumFreeDiskMegabytes < 0)
                throw new ArgumentOutOfRangeException(nameof(MinimumFreeDiskMegabytes), MinimumFreeDiskMegabytes, "O espaço mínimo em disco não pode ser negativo.");

            if (MaxConcurrentConversions < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxConcurrentConversions), MaxConcurrentConversions, "É preciso permitir ao menos uma conversão simultânea.");

            if (OrphanProfileMaxAge < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(OrphanProfileMaxAge), OrphanProfileMaxAge, "A idade máxima de profiles órfãos não pode ser negativa.");

            if (OrphanCleanupInterval < TimeSpan.Zero && OrphanCleanupInterval != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(OrphanCleanupInterval), OrphanCleanupInterval, "O intervalo de limpeza de órfãos não pode ser negativo.");

            // One --user-data-dir cannot back two Chromium processes at once: the second either dies on
            // the SingletonLock or hands the work to the first and exits, and the conversion comes back
            // empty with no error at all. It is the combination that is invalid, not either value.
            if (!string.IsNullOrEmpty(UserProfileDirectory) && MaxConcurrentConversions > 1)
                throw new ArgumentException(
                    "UserProfileDirectory e MaxConcurrentConversions > 1 são incompatíveis: um profile compartilhado não " +
                    "suporta duas instâncias do Chromium ao mesmo tempo. Deixe MaxConcurrentConversions em 1 ou volte ao " +
                    "profile descartável.",
                    nameof(MaxConcurrentConversions));

            // The converter appends its own --user-data-dir after this list, precisely so it always
            // has the final word - see ChromiumHtmlToPdfConverter.ConvertAsync. An entry here would
            // either be silently overwritten (surprising) or, if the append order ever changed, would
            // point Chromium at a directory the converter does not own, does not lock and does not
            // clean up. UserProfileDirectory is the supported way to pin the profile.
            foreach (var argument in ChromiumArguments)
            {
                if (argument != null && argument.StartsWith("--user-data-dir", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(
                        "ChromiumArguments não pode conter --user-data-dir; use UserProfileDirectory para fixar o " +
                        "profile.",
                        nameof(ChromiumArguments));
            }

            if (PageSettings == null)
                throw new ArgumentNullException(nameof(PageSettings), "As configurações de página não podem ser nulas.");
        }
    }
}
