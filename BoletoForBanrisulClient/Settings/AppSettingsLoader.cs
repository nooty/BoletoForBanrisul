using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BoletoForBanrisul.Client.Settings
{
    public class AppSettingsLoader
    {
        private readonly string _resourceName = "BoletoForBanrisul.Client.Resources.UrlSettings.json";

        public AppSettings LoadSettings(ApiEnvironmentType environment)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Tenta acessar o recurso embutido.
            using var stream = assembly.GetManifestResourceStream(_resourceName);
            
            if (stream == null)
            {
                throw new FileNotFoundException($"Settings - Recurso '{_resourceName}' não encontrado.");
            }

            using var reader = new StreamReader(stream);
                
            var json = reader.ReadToEnd();
            var settingsContainer = JsonSerializer.Deserialize<AppSettingsContainer>(json);

            if (settingsContainer == null)
            {
                throw new InvalidOperationException("Settings - Recurso inválido.");
            }

            // Seleciona as configurações apropriadas com base no ambiente.
            var settings = environment == ApiEnvironmentType.Production
                ? settingsContainer.Production
                : settingsContainer.Sandbox;

            settings.Environment = environment;

            return settings;
        }
    }
}
