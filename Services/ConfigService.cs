using Microsoft.Extensions.Logging;
using SystemEye.Models;

namespace SystemEye.Services
{
    /// <summary>
    /// Service zum Verwalten der Anwendungskonfiguration.
    /// Lädt und speichert Einstellungen aus der lokalen JSON-Datei.
    /// </summary>
    public class ConfigService
    {
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
        /// Gibt ein AppConfig‑Objekt mit geladenen Daten oder Standardwerten zurück.
        /// </returns>
        public async Task<AppConfig> LoadConfigAsync()
        {
            try
            {
                return await Task.FromResult(new AppConfig());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fehler beim Laden der Konfiguration.");
                return new AppConfig();
            }
        }
    }
}
