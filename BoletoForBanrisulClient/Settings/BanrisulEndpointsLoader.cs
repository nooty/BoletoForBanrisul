using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.Settings
{
    /// <summary>Loads the embedded <c>Resources/UrlSettings.json</c> resource into the <see cref="BanrisulEndpoints"/> for one host.</summary>
    /// <remarks>
    /// Internal: nothing on the public surface exposes it. The builder calls it while resolving
    /// <c>UseSandbox</c>, and a consumer never needs to load endpoints themselves.
    /// </remarks>
    internal class BanrisulEndpointsLoader
    {
        /// <summary>Loads the hostname and paths for the given host.</summary>
        /// <remarks>
        /// The embedded resource is located by the suffix <c>Resources.UrlSettings.json</c> rather than by a
        /// hardcoded full manifest resource name, because <c>RootNamespace</c> (and therefore the manifest
        /// resource name) can change independently of the file's on-disk path - a mismatch here would only
        /// surface at runtime, not at build time.
        /// </remarks>
        public BanrisulEndpoints LoadSettings(ApiHost host)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Locates the embedded resource by suffix.
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("Resources.UrlSettings.json", StringComparison.Ordinal))
                ?? throw new FileNotFoundException(
                    $"Settings - UrlSettings.json not found. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}.");

            using var stream = assembly.GetManifestResourceStream(resourceName)!;

            using var reader = new StreamReader(stream);

            var json = reader.ReadToEnd();
            var settingsContainer = JsonSerializer.Deserialize<BanrisulEndpointsContainer>(json);

            if (settingsContainer == null)
            {
                throw new InvalidOperationException("Settings - invalid resource.");
            }

            // Selects the settings for the requested host.
            var settings = host == ApiHost.Production
                ? settingsContainer.Production
                : settingsContainer.Sandbox;

            settings.Host = host;

            return settings;
        }
    }
}
