using BoletoForBanrisul.Report.ViewModels;
using RazorLight;
using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace BoletoForBanrisul.Report
{
    /// <summary>
    /// Renderizador padrão, baseado em RazorLight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Construir custa caro - o RazorLight resolve as referências de metadados do Roslyn e cria um
    /// cache de templates vazio - e uma instância por boleto recompila o template a cada impressão.
    /// Use uma só: <see cref="Default"/> para o template embutido, ou uma sua como singleton.
    /// </para>
    /// <para>
    /// É seguro para uso concorrente: o RazorLight sincroniza a compilação por chave de template e
    /// entrega uma instância de página nova a cada renderização.
    /// </para>
    /// </remarks>
    public sealed class RazorBoletoTemplateRenderer : IBoletoTemplateRenderer
    {
        private const string ResourcesNamespace = "BoletoForBanrisul.Report.Resources";
        private const string EmbeddedTemplateName = "Boleto.cshtml";

        private static readonly Lazy<RazorBoletoTemplateRenderer> _default =
            new Lazy<RazorBoletoTemplateRenderer>(() => new RazorBoletoTemplateRenderer(), LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly RazorLightEngine _engine;
        private readonly string _templateKey;

        /// <summary>
        /// Cria o renderizador com o template de boleto embutido no pacote.
        /// </summary>
        public RazorBoletoTemplateRenderer()
        {
            _engine = new RazorLightEngineBuilder()
                .UseEmbeddedResourcesProject(typeof(RazorBoletoTemplateRenderer).Assembly, ResourcesNamespace)
                .SetOperatingAssembly(Assembly.GetExecutingAssembly())
                .UseMemoryCachingProvider()
                .Build();

            _templateKey = EmbeddedTemplateName;
        }

        /// <summary>
        /// Cria o renderizador com um template próprio, lido do sistema de arquivos.
        /// </summary>
        /// <param name="templateDirectoryPath">Diretório onde o template está armazenado.</param>
        /// <param name="templateFileName">Nome do arquivo de template.</param>
        public RazorBoletoTemplateRenderer(string templateDirectoryPath, string templateFileName)
        {
            if (string.IsNullOrEmpty(templateDirectoryPath))
                throw new ArgumentException("O diretório do template do boleto não foi informado.", nameof(templateDirectoryPath));

            if (string.IsNullOrEmpty(templateFileName))
                throw new ArgumentException("O nome do arquivo de template do boleto não foi informado.", nameof(templateFileName));

            if (!Directory.Exists(templateDirectoryPath))
                throw new InvalidOperationException($"Caminho do arquivo de template inválido. Path={templateDirectoryPath}");

            var templateFilePath = Path.Combine(templateDirectoryPath, templateFileName);

            if (!File.Exists(templateFilePath))
                throw new InvalidOperationException($"Arquivo de template do boleto inválido. File={templateFilePath}");

            _engine = new RazorLightEngineBuilder()
                .UseFileSystemProject(templateDirectoryPath)
                .SetOperatingAssembly(Assembly.GetExecutingAssembly())
                .UseMemoryCachingProvider()
                .Build();

            _templateKey = templateFileName;
        }

        /// <summary>
        /// Instância compartilhada para o template embutido, criada na primeira utilização.
        /// </summary>
        /// <remarks>
        /// É o padrão do <see cref="BoletoReportBuilder"/>, para que vários relatórios não custem um
        /// engine Razor cada um mesmo sem injeção de dependência.
        /// </remarks>
        public static RazorBoletoTemplateRenderer Default => _default.Value;

        /// <inheritdoc />
        public async Task<string> RenderAsync(PrintViewModel viewModel, CancellationToken cancellationToken = default)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            cancellationToken.ThrowIfCancellationRequested();

            // Sem lock: o RazorTemplateCompiler já serializa a compilação por chave, e o cache de
            // templates é um IMemoryCache.
            return await _engine.CompileRenderAsync(_templateKey, viewModel).ConfigureAwait(false);
        }
    }
}
