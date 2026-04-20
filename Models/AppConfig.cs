using System;
using System.Collections.Generic;
using System.Text;

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
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 3306; //Standardport
        public string DatabaseName { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public string GetConnectionString()
        {
            return $"Server={Server};Port={Port};Database={DatabaseName};Uid={User};Pwd={Password}";
        }
    }
}
