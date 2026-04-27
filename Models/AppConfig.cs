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
    /// Repräsentiert die Datenbankeinstellungen der Anwendung (SQLite)
    /// und bietet eine Methode zum Erzeugen des Connection‑Strings.
    /// </summary>
    /// /// <returns>
    /// Gibt den Connection-String zurück.
    /// </returns>
    public class DatabaseConfig
    {
        public string Database { get; set; } = "SystemEye_Data.db";

        public string GetConnectionString()
        {
            return $"Data Source={Database}";
        }
    }
}
