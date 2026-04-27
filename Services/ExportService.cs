using Microsoft.Extensions.Logging;
using System.IO;
using SystemEye.Models;

namespace SystemEye.Services
{
    /// <summary>
    /// Service für den Datenexport.
    /// Ermöglicht die Ausgabe von Sensordaten in externe Dateiformate.
    /// </summary>
    public class ExportService
    {
        private readonly ILogger<ExportService> _logger;

        public ExportService(ILogger<ExportService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Exportiert aggregierte Sensordaten in eine strukturierte Textdatei.
        /// Gruppiert die Daten nach Hardware und berechnet Gesamtstatistiken für den Export.
        /// </summary>
        /// <param name="filePath">Zielpfad der Datei.</param>
        /// <param name="data">Die zu exportierende Liste von AggregatedSensorData.</param>
        /// <returns>
        /// Ein Task für den asynchronen Ablauf.
        /// </returns>
        public async Task ExportDatabaseDataToTxtAsync(string filePath, List<AggregatedSensorData> data)
        {
            try
            {
                using var writer = new StreamWriter(filePath, append: false); // false == überschreibt existierende Datei

                await writer.WriteLineAsync("==================================================");
                await writer.WriteLineAsync("   SYSTEMEYE - HISTORIE-EXPORT (LETZTE STUNDE)   ");
                await writer.WriteLineAsync("==================================================");
                await writer.WriteLineAsync($"Erstellt am: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                await writer.WriteLineAsync(new string('-', 50));

                if (data == null || data.Count == 0)
                {
                    await writer.WriteLineAsync("Keine Datenbank-Einträge für den gewählten Zeitraum gefunden.");
                    return;
                }
                var grouped = data.GroupBy(d => new { d.HardwareType, d.Name, d.Format });

                foreach (var group in grouped)
                {
                    // Filtert Null-Werte aus, um die Durchschnittsberechnung nicht zu verfälschen
                    var nonZeroEntries = group.Where(g => g.MinValue > 0.001).ToList();

                    var calculationBase = nonZeroEntries.Any() ? nonZeroEntries : group.ToList();

                    double totalAvg = calculationBase.Average(g => g.AvgValue);
                    double totalMin = calculationBase.Min(g => g.MinValue);
                    double totalMax = group.Max(g => g.MaxValue);

                    await writer.WriteLineAsync($"Hardware: {group.Key.HardwareType}");
                    await writer.WriteLineAsync($"Sensor:   {group.Key.Name}");
                    await writer.WriteLineAsync($"Statistik: Ø {totalAvg:F2} {group.Key.Format}");
                    await writer.WriteLineAsync($"Bereich:   Min {totalMin:F1} / Max {totalMax:F1}");
                    await writer.WriteLineAsync(new string('-', 30));
                }

                await writer.WriteLineAsync($"\nExport von {data.Count} Datensätzen erfolgreich abgeschlossen.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Fehler beim Schreiben der Export-Datei: {ex.Message}");
            }
        }
    }
}
