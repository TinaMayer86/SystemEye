using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using SystemEye.Models;

namespace SystemEye.Services
{
    public class ConfigService
    {
        private readonly string _configPath = Path.Combine("Config", "config.json");
        private readonly ILogger<ConfigService>? _logger;

        public ConfigService(ILogger<ConfigService>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Lädt die Anwendungskonfiguration aus der config.json, 
        /// protokolliert Warnungen oder Fehler bei fehlenden oder fehlerhaften Dateien 
        /// und gibt entweder die gelesenen Einstellungen oder Standardwerte zurück.
        /// </summary>
        /// <returns>
        /// Gibt ein AppConfig‑Objekt zurück, das entweder aus der config.json geladen 
        /// wurde oder Standardwerte enthält, falls die Datei fehlt, leer ist oder nicht korrekt
        /// verarbeitet werden konnte.
        /// </returns>
        public async Task<AppConfig> LoadConfigAsync()
        {
            if (!File.Exists(_configPath))
            {
                _logger?.LogWarning("Konfigurationsdatei nicht gefunden: {ConfigPath}", _configPath);
                return new AppConfig();
            }

            try
            {
                string jsonString = await File.ReadAllTextAsync(_configPath);

                // WICHTIG: PropertyNameCaseInsensitive auf true setzen!
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var config = JsonSerializer.Deserialize<AppConfig>(jsonString, options);

                if (config == null) return new AppConfig();

                return config;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim Laden der Konfiguration.");
                return new AppConfig();
            }
        }
    }
}
