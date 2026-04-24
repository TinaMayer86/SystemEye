namespace SystemEye.Models
{
    /// <summary>
    /// Enthält die Anwendungskonfiguration und stellt die Datenbankeinstellungen 
    /// über das Database‑Objekt bereit.
    /// </summary>
    public class AppConfig
    {
        public DatabaseConfig Database { get; set; } = new();
    }

    /// <summary>
    /// Repräsentiert die Datenbankeinstellungen der Anwendung, 
    /// einschließlich Server, Port, Datenbankname und Zugangsdaten, 
    /// und bietet eine Methode zum Erzeugen des Connection‑Strings.
    /// </summary>
    /// <returns>
    /// Gibt einen vollständigen MySQL‑Connection‑String basierend auf den 
    /// gespeicherten Datenbankeinstellungen zurück.
    /// </returns>
    public class DatabaseConfig
    {
        public string FilePath { get; set; } = string.Empty;

        public string GetConnectionString()
        {
            return $"Data Source={FilePath}";
        }
    }
}
