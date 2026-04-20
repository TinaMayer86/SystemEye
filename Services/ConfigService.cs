using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SystemEye.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
                _logger?.LogWarning("Die Konfigurationsdatei wurde unter {ConfigPath} nicht gefunden. Es werden Standardwerte verwendet.", _configPath);
            }

            try
            {
                string jsonString = await File.ReadAllTextAsync(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(jsonString);

                if(config == null)
                {
                    _logger?.LogError("Die config.json war leer. Es werden Standardwerte verwendet.");
                    return new AppConfig();
                }
                return config;
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "Formatierungsfehler in der config.json! Bitte prüfe die Datei auf fehlende Kommas oder Klammern. Standardwerte werden geladen.");
                return new AppConfig();
            }
            catch(Exception ex)
            {
                _logger?.LogError(ex, "Unbekannter Fehler beim Lesen der Konfigurationsdatei.");
                return new AppConfig();
            }
        }
    }
}
