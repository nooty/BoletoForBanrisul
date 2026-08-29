using BoletoForBanrisul.Report.ViewModels;
using RazorLight;
using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace BoletoForBanrisul.Report
{
    /// <summary>
    /// Default renderer, based on RazorLight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Building it is expensive - RazorLight resolves Roslyn's metadata references and creates an empty
    /// template cache - and one instance per boleto recompiles the template on every print. Use a single
    /// one: <see cref="Default"/> for the embedded template, or one of your own as a singleton.
    /// </para>
    /// <para>
    /// It is safe for concurrent use: RazorLight synchronises compilation per template key and hands out
    /// a fresh page instance on every render.
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
        /// Creates the renderer with the boleto template embedded in the package.
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
        /// Creates the renderer with a template of your own, read from the file system.
        /// </summary>
        /// <param name="templateDirectoryPath">Directory the template is stored in.</param>
        /// <param name="templateFileName">Template file name.</param>
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
        /// Shared instance for the embedded template, created on first use.
        /// </summary>
        /// <remarks>
        /// It is <see cref="BoletoReportBuilder"/>'s default, so that several reports do not cost one
        /// Razor engine each even without dependency injection.
        /// </remarks>
        public static RazorBoletoTemplateRenderer Default => _default.Value;

        /// <inheritdoc />
        /// <remarks>
        /// Cancellation only applies at the edges. RazorLight's <c>CompileRenderAsync</c> takes no
        /// <see cref="CancellationToken"/>, so compiling the template - the expensive part, and only on
        /// the first call for each key - runs to completion even after the token is cancelled. The
        /// token is checked before and after it, so a cancellation does not go unnoticed, but an
        /// immediate abort during the first render is not available.
        /// </remarks>
        public async Task<string> RenderAsync(PrintViewModel viewModel, CancellationToken cancellationToken = default)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            cancellationToken.ThrowIfCancellationRequested();

            // No lock: RazorTemplateCompiler already serialises compilation per key, and the template
            // cache is an IMemoryCache.
            var html = await _engine.CompileRenderAsync(_templateKey, viewModel).ConfigureAwait(false);

            // Again after the await: compilation does not observe the token, so without this second
            // check a cancellation requested during it would only be noticed later, in the converter.
            cancellationToken.ThrowIfCancellationRequested();

            return html;
        }
    }
}
