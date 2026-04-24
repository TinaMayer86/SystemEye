using Microsoft.Extensions.Logging;
using System.IO;
using SystemEye.Models;

namespace SystemEye.Services
{
    public class ExportService
    {
        private readonly ILogger<ExportService> _logger;

        public ExportService(ILogger<ExportService> logger)
        {
            _logger = logger;
        }

        public async Task ExportCurrentDataToTxtAsync(string filePath, List<SensorDataModel> sensors)
        {
            try
            {
                using var writer = new StreamWriter(filePath, append: false); // false == Alte Datei überschreiben

                await writer.WriteLineAsync("--- SystemEye Export ---");
                await writer.WriteLineAsync($"Erstellt am: {DateTime.Now:dd.MM.yyyy}");
                await writer.WriteLineAsync(new string('-', 24));

                if (sensors == null || sensors.Count == 0)
                {
                    await writer.WriteLineAsync("Keine Sensordaten zum Exportieren vorhanden");
                    return;
                }

                foreach (var sensor in sensors)
                {
                    await writer.WriteLineAsync($"Hardware: {sensor.HardwareType}");
                    await writer.WriteLineAsync($"Sensor: {sensor.Name}");
                    await writer.WriteLineAsync($"Typ: {sensor.SensorType}");
                    await writer.WriteLineAsync($"Wert: {sensor.Value:F2} {sensor.Format}");
                    await writer.WriteLineAsync(new string('-', 30));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Exportieren der Sensordaten in die Datei {FilePath}.", filePath);
                throw;
            }
        }
    }
}
