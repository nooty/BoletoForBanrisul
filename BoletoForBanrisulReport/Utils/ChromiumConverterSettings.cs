using ChromiumHtmlToPdfLib.Enums;
using ChromiumHtmlToPdfLib.Settings;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace BoletoForBanrisul.Report.Utils
{
    /// <summary>
    /// Configurações do <see cref="ChromiumHtmlToPdfConverter"/>.
    /// </summary>
    /// <remarks>
    /// Os padrões são conservadores de propósito: profile descartável por conversão, sem cache,
    /// timeout curto e pré-checagem de disco.
    /// </remarks>
    public class ChromiumConverterSettings
    {
        /// <summary>
        /// Argumentos passados ao Chromium. Já vem preenchida com os padrões; adicione ou remova
        /// itens em vez de substituir a lista.
        /// </summary>
        /// <remarks>
        /// Os cinco primeiros são os necessários para rodar headless em container; os demais calam
        /// o ruído de rede, GCM e atualização de componentes, típico de um Linux sem sessão gráfica.
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
        /// Tempo máximo que o Chromium tem para concluir uma conversão. Padrão: 2 minutos.
        /// </summary>
        public TimeSpan ConversionTimeout { get; set; } = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Folga adicional depois de <see cref="ConversionTimeout"/> antes de o conversor derrubar
        /// o Chromium à força. Padrão: 30 segundos.
        /// </summary>
        /// <remarks>
        /// Rede de segurança para quando o Chromium pendura num nível que não devolve o
        /// <c>await</c>. Nesse caso o <see cref="ConversionTimeout"/> não tem efeito, porque não há
        /// ninguém observando o cancelamento, e só matar o processo resolve.
        /// </remarks>
        public TimeSpan HardTimeoutGrace { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Diretório de profile do Chromium (<c>--user-data-dir</c>). Quando nulo ou vazio (padrão),
        /// cada conversão usa um profile próprio em diretório temporário, apagado ao final.
        /// </summary>
        /// <remarks>
        /// Preenchido, todas as conversões passam a compartilhar o mesmo profile: ele cresce sem
        /// limite e, se for corrompido por um Chromium morto no meio de uma conversão, continua
        /// quebrando as conversões seguintes. Use apenas com um motivo concreto.
        /// </remarks>
        public string? UserProfileDirectory { get; set; }

        /// <summary>
        /// Habilita o cache em disco do Chromium. Padrão: <c>false</c>.
        /// </summary>
        public bool UseCache { get; set; } = false;

        /// <summary>
        /// Espaço livre mínimo, em megabytes, exigido antes de subir o Chromium. Padrão: 500.
        /// Use <c>0</c> para desligar a checagem.
        /// </summary>
        /// <remarks>
        /// Abaixo do limite a conversão falha imediatamente com
        /// <see cref="Exceptions.InsufficientDiskSpaceException"/>. No dobro do limite é emitido um
        /// aviso, para dar tempo de reagir antes de o disco acabar de fato.
        /// </remarks>
        public int MinimumFreeDiskMegabytes { get; set; } = 500;

        /// <summary>
        /// Quantidade de conversões simultâneas permitidas por instância do conversor. Padrão: 1.
        /// </summary>
        /// <remarks>
        /// Cada conversão simultânea é um processo Chromium a mais, com o custo correspondente de
        /// memória e disco. Aumente só depois de medir.
        /// </remarks>
        public int MaxConcurrentConversions { get; set; } = 1;

        /// <summary>
        /// Caminho do executável do Chrome/Chromium. Quando nulo, a localização é automática.
        /// </summary>
        public string? ChromiumExecutablePath { get; set; }

        /// <summary>
        /// Navegador a ser usado. Padrão: <see cref="ChromiumHtmlToPdfLib.Enums.Browser.Chrome"/>.
        /// </summary>
        public Browser Browser { get; set; } = Browser.Chrome;

        /// <summary>
        /// Configurações de página (papel, margens, orientação) usadas na impressão.
        /// </summary>
        public PageSettings PageSettings { get; set; } = new PageSettings();

        /// <summary>
        /// Valor de <c>window.status</c> a ser aguardado antes de imprimir. Nulo (padrão) não espera.
        /// </summary>
        public string? WaitForWindowStatus { get; set; }

        /// <summary>
        /// Tempo máximo, em milissegundos, de espera por <see cref="WaitForWindowStatus"/>. Padrão: 60000.
        /// </summary>
        public int WaitForWindowStatusTimeout { get; set; } = 60000;

        /// <summary>
        /// Tempo máximo, em milissegundos, de espera pelo carregamento de mídia. Nulo (padrão) usa o
        /// comportamento da biblioteca.
        /// </summary>
        public int? MediaLoadTimeout { get; set; }

        /// <summary>
        /// Logger opcional, repassado à biblioteca de conversão e usado pelo próprio conversor.
        /// </summary>
        /// <remarks>
        /// Sem ele, uma conversão presa é indistinguível de uma conversão lenta. Vale a pena
        /// configurar em qualquer ambiente não interativo.
        /// </remarks>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Idade a partir da qual um profile temporário esquecido é considerado órfão e apagado.
        /// Padrão: 1 hora.
        /// </summary>
        /// <seealso cref="ChromiumHtmlToPdfConverter.CleanupOrphanedProfiles(TimeSpan, ILogger)"/>
        public TimeSpan OrphanProfileMaxAge { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Valida os valores configurados. Chamado pelo construtor do conversor.
        /// </summary>
        internal void Validate()
        {
            if (ConversionTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(ConversionTimeout), ConversionTimeout, "O timeout de conversão deve ser maior que zero.");

            if (HardTimeoutGrace < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(HardTimeoutGrace), HardTimeoutGrace, "A folga do deadline não pode ser negativa.");

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

            if (PageSettings == null)
                throw new ArgumentNullException(nameof(PageSettings), "As configurações de página não podem ser nulas.");
        }
    }
}
