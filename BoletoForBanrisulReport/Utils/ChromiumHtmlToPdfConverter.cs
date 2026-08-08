using BoletoForBanrisul.Report.Exceptions;
using ChromiumHtmlToPdfLib;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace BoletoForBanrisul.Report.Utils
{
    /// <summary>
    /// Conversor de HTML para PDF via Chrome/Chromium headless.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cada conversão roda em um profile próprio e descartável, sem cache e sem nada compartilhado
    /// entre chamadas, para que um Chromium morto no meio de uma conversão não contamine a próxima.
    /// Antes de subir o navegador é feita uma pré-checagem de espaço em disco: sem espaço o Chromium
    /// pendura no meio da conversão em vez de retornar erro.
    /// </para>
    /// <para>
    /// O <see cref="CancellationToken"/> é honrado do começo ao fim - inclusive na espera por outra
    /// conversão em andamento - e há um deadline duro que derruba o processo quando nem o
    /// cancelamento é observado.
    /// </para>
    /// <para>
    /// A instância é segura para uso concorrente e cara de criar: use uma só. Registrada no contêiner
    /// de DI, o descarte é do contêiner; criada na mão, chame <see cref="Dispose"/> ao final.
    /// </para>
    /// </remarks>
    public sealed class ChromiumHtmlToPdfConverter : IPdfConverter, IDisposable
    {
        private const string ProfilesDirectoryName = "boletoforbanrisul-chromium-profiles";

        // Espera entre as tentativas de apagar o profile da conversão. Ver DeleteProfileDirectoryAsync.
        private static readonly TimeSpan[] ProfileDeleteRetryDelays =
        {
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(150),
            TimeSpan.FromMilliseconds(400)
        };

        // A varredura percorre o diretório temporário inteiro, então é compartilhada por todas as
        // instâncias: uma varredura de cada vez, por processo.
        private static int _orphanCleanupRunning;

        // Relógio da última varredura concluída; long.MinValue significa "nunca varreu". Stopwatch e
        // não DateTime porque o relógio do sistema pode andar para trás e travar a limpeza para
        // sempre.
        private static long _lastOrphanCleanupTimestamp = long.MinValue;

        private readonly ChromiumConverterSettings _settings;
        private readonly SemaphoreSlim _conversionSemaphore;
        private readonly ILogger? _logger;

        private bool _disposed;

        /// <summary>
        /// Cria o conversor com as configurações padrão.
        /// </summary>
        public ChromiumHtmlToPdfConverter() : this(new ChromiumConverterSettings())
        {
        }

        /// <summary>
        /// Cria o conversor aplicando uma ação de configuração sobre os valores padrão.
        /// </summary>
        /// <param name="configureSettings">Ação que ajusta as configurações.</param>
        public ChromiumHtmlToPdfConverter(Action<ChromiumConverterSettings> configureSettings)
            : this(Configure(configureSettings))
        {
        }

        /// <summary>
        /// Cria o conversor com as configurações fornecidas.
        /// </summary>
        /// <param name="settings">Configurações do conversor.</param>
        public ChromiumHtmlToPdfConverter(ChromiumConverterSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
        /// Diretório raiz onde ficam os profiles temporários criados pelo conversor.
        /// </summary>
        public static string ProfilesRootDirectory => Path.Combine(Path.GetTempPath(), ProfilesDirectoryName);

        /// <inheritdoc />
        /// <exception cref="InsufficientDiskSpaceException">Não há espaço livre suficiente para subir o Chromium.</exception>
        /// <exception cref="TimeoutException">A conversão estourou o deadline interno sem que o chamador tenha cancelado.</exception>
        /// <exception cref="PdfConversionException">A conversão falhou por qualquer outro motivo.</exception>
        public async Task<byte[]> ConvertToPdfAsync(string html, CancellationToken cancellationToken = default)
        {
            if (html == null)
                throw new ArgumentNullException(nameof(html));

            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            // Fora do semáforo: a limpeza não disputa o slot de conversão.
            ScheduleOrphanCleanupIfDue();

            await _conversionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                ThrowIfDisposed();

                EnsureFreeDiskSpace();

                return await ConvertAsync(html, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _conversionSemaphore.Release();
            }
        }

        private async Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken)
        {
            var isDisposableProfile = string.IsNullOrEmpty(_settings.UserProfileDirectory);

            var profileDirectory = isDisposableProfile
                ? Path.Combine(ProfilesRootDirectory, Guid.NewGuid().ToString("N"))
                : _settings.UserProfileDirectory!;

            Directory.CreateDirectory(profileDirectory);

            // O deadline duro fica depois do timeout da própria conversão, para que o Chromium
            // tenha a chance de abortar sozinho - com log e limpeza - antes de ser derrubado.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_settings.ConversionTimeout + _settings.HardTimeoutGrace);

            var handle = new ConverterHandle(
                new Converter(
                    chromiumExeFileName: _settings.ChromiumExecutablePath,
                    userProfile: profileDirectory,
                    logger: _logger,
                    useCache: _settings.UseCache,
                    browser: _settings.Browser),
                _logger);

            try
            {
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
                    conversionTimeout: (int)_settings.ConversionTimeout.TotalMilliseconds,
                    mediaLoadTimeout: _settings.MediaLoadTimeout,
                    logger: _logger,
                    cancellationToken: deadline.Token);

                await AwaitWithHardDeadline(conversion, handle, deadline.Token).ConfigureAwait(false);

                return byteStream.ToArray();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested)
            {
                // Cancelada no meio da partida do Chromium, a biblioteca relata a interrupção com o
                // que estiver à mão - NullReferenceException do handshake do DevTools abortado,
                // ConversionException, TaskCanceledException - conforme o ponto exato em que o token
                // pegou. Quem cancelou recebe cancelamento; o que a biblioteca lançou fica no log e
                // na InnerException.
                _logger?.LogWarning(exception,
                    "A conversão cancelada veio à tona como {exceptionType}; tratada como cancelamento.",
                    exception.GetType().Name);

                throw new OperationCanceledException("A conversão para PDF foi cancelada.", exception, cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                throw new TimeoutException(
                    $"A conversão para PDF não terminou dentro de {_settings.ConversionTimeout.TotalSeconds:0.#}s (+{_settings.HardTimeoutGrace.TotalSeconds:0.#}s de folga) e foi abortada.",
                    exception);
            }
            catch (Exception exception)
            {
                throw new PdfConversionException("Falha ao converter o HTML do boleto em PDF.", exception);
            }
            finally
            {
                handle.Dispose();

                if (isDisposableProfile)
                    await DeleteProfileDirectoryAsync(profileDirectory).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Espera a conversão respeitando o deadline, e derruba o Chromium se ele não responder.
        /// </summary>
        /// <remarks>
        /// Cancelamento é cooperativo: o token só marca o estado como "cancelado", alguém precisa
        /// observá-lo. Se o Chromium pendura num nível que não devolve o <c>await</c> - websocket do
        /// devtools mudo, processo filho zumbi - ninguém observa e a <see cref="Task"/> nunca
        /// completa. Descartar o <c>Converter</c> mata o processo e faz a conversão presa falhar, que
        /// é o único remédio nesse caso.
        /// </remarks>
        private async Task AwaitWithHardDeadline(Task conversion, ConverterHandle handle, CancellationToken deadlineToken)
        {
            // Fonte própria, cancelada no finally, para a espera terminar junto com a conversão em
            // vez de ficar pendente.
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

                deadlineToken.ThrowIfCancellationRequested();
            }
            finally
            {
                deadlineWait.Cancel();
            }
        }

        /// <summary>
        /// Falha rápido quando o disco está cheio, em vez de deixar o Chromium pendurar no meio da
        /// conversão.
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
                // A checagem é proteção extra, não pré-requisito: não conseguir medir não deve
                // recusar a conversão.
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

            // Em Linux o root de qualquer caminho absoluto é "/", que é sempre um drive válido.
            return new DriveInfo(string.IsNullOrEmpty(root) ? "/" : root).AvailableFreeSpace;
        }

        /// <summary>
        /// Apaga profiles temporários deixados para trás por processos que morreram no meio de uma
        /// conversão.
        /// </summary>
        /// <param name="olderThan">Idade mínima para um profile ser considerado órfão.</param>
        /// <param name="logger">Logger opcional para registrar falhas de limpeza.</param>
        /// <remarks>
        /// Só remove profiles parados há mais tempo que <paramref name="olderThan"/>: o diretório
        /// temporário pode ser compartilhado por mais de um processo, e apagar a raiz inteira
        /// derrubaria a conversão em andamento do vizinho.
        /// </remarks>
        public static void CleanupOrphanedProfiles(TimeSpan olderThan, ILogger? logger = null)
        {
            var profilesRoot = ProfilesRootDirectory;

            if (!Directory.Exists(profilesRoot))
                return;

            var cutoff = DateTime.UtcNow - olderThan;

            IEnumerable<string> profiles;

            try
            {
                profiles = Directory.EnumerateDirectories(profilesRoot);
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
                    if (Directory.GetLastWriteTimeUtc(profile) >= cutoff)
                        continue;

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
        /// Dispara a varredura de profiles órfãos quando ela está vencida.
        /// </summary>
        /// <remarks>
        /// Fora do semáforo e em background: enumerar e apagar diretórios é E/S que não tem por que
        /// entrar no tempo de resposta de quem pediu o PDF, ainda mais agora que se repete. Quem
        /// dispara é a conversão, e não um timer: profile órfão só nasce de uma conversão, então um
        /// processo ocioso não tem o que limpar.
        /// </remarks>
        private void ScheduleOrphanCleanupIfDue()
        {
            if (!IsOrphanCleanupDue(_settings.OrphanCleanupInterval))
                return;

            // Duas varreduras simultâneas disputariam os mesmos diretórios e só renderiam avisos de
            // remoção falha.
            if (Interlocked.CompareExchange(ref _orphanCleanupRunning, 1, 0) != 0)
                return;

            var maxAge = EffectiveOrphanProfileMaxAge;
            var logger = _logger;

            // Thread própria e não o pool: a varredura é E/S bloqueante de duração desconhecida, e o
            // pool é o mesmo que carrega as continuações da conversão. Uma thread por hora é barato
            // demais para valer o risco de disputar o pool com quem está esperando um PDF.
            _ = Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        CleanupOrphanedProfiles(maxAge, logger);
                    }
                    catch (Exception exception)
                    {
                        // Ninguém observa esta Task: uma exceção não capturada aqui sobe como
                        // UnobservedTaskException e pode derrubar o host.
                        logger?.LogWarning(exception, "Falha na limpeza de profiles órfãos do Chromium.");
                    }
                    finally
                    {
                        // Carimba no fim, não no começo: uma varredura lenta não deve vencer sozinha.
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
        /// Idade mínima efetiva para considerar um profile órfão: nunca menor que o deadline da
        /// conversão.
        /// </summary>
        /// <remarks>
        /// Enquanto a varredura rodava uma vez por processo, ela acontecia antes de qualquer profile
        /// existir. Rodando em paralelo com as conversões, um
        /// <see cref="ChromiumConverterSettings.OrphanProfileMaxAge"/> curto passaria a apagar o
        /// <c>--user-data-dir</c> de uma conversão em andamento debaixo do Chromium. Um profile mais
        /// novo que <c>ConversionTimeout + HardTimeoutGrace</c> ainda pode ter dono, então não é
        /// órfão.
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
        /// Remove o profile descartável da conversão, com retentativas curtas.
        /// </summary>
        /// <remarks>
        /// Matar o Chromium não devolve os handles na mesma hora: o Crashpad mantém o
        /// <c>CrashpadMetrics-active.pma</c> - 1 MB, mapeado em memória - preso por um instante
        /// depois de o processo morrer, e a remoção falha com acesso negado justo nesse arquivo,
        /// depois de já ter apagado todo o resto do profile. A espera é curta - na prática o handle
        /// já saiu na primeira retentativa - mas sem ela sobra 1 MB por conversão.
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
                    // Sem o token: cancelar a conversão não é motivo para deixar o profile para trás.
                    await Task.Delay(ProfileDeleteRetryDelays[attempt]).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    // O profile fica para a limpeza de órfãos; perder a limpeza não pode custar o PDF
                    // já gerado.
                    _logger?.LogWarning(exception, "Não foi possível remover o profile do Chromium {directory}.", directory);
                    return;
                }
            }
        }

        private void ObserveInBackground(Task task)
        {
            // A conversão abandonada ainda vai falhar quando o processo morrer; sem observar a
            // exceção, ela sobe como UnobservedTaskException e pode derrubar o host. O log em Debug
            // preserva o que a biblioteca lançou de fato, que aqui é esperado e não é erro.
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
        /// Libera os recursos do conversor.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _conversionSemaphore.Dispose();
        }

        /// <summary>
        /// Envolve o <see cref="Converter"/> para que o descarte seja idempotente: ele é chamado
        /// tanto no caminho do deadline - onde matar o Chromium é a ação - quanto no <c>finally</c>.
        /// </summary>
        private sealed class ConverterHandle : IDisposable
        {
            private readonly ILogger? _logger;
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

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                    return;

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
