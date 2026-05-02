namespace SystemEye.Models
{
    /// <summary>
    /// Verwaltet die Anwendungskonfiguration und stellt den Pfad zur lokalen 
    /// Datenbankdatei sowie den daraus abgeleiteten Connection‑String bereit.
    /// </summary>
    public class AppConfig
    {
        public string Database { get; set; } = "SystemEye_Data.db";
        public string GetConnectionString()
        {
            return $"Data Source={Database}";
        }
    }
}
