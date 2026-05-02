using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using SystemEye.Models;

namespace SystemEye.Services
{
    /// <summary>
    /// Service zur Auslesung von Hardware-Informationen.
    /// Nutzt LibreHardwareMonitor, um Sensordaten und Systemkomponenten abzufragen.
    /// </summary>
    public class HardwareService
    {
        private readonly Computer _computer;
        private readonly ILogger<HardwareService> _logger;

        public HardwareService(ILogger<HardwareService> logger)
        {
            _logger = logger;
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true,
                IsControllerEnabled = true
            };
            _computer.Open();
        }

        /// <summary>
        /// Ruft allgemeine Systeminformationen wie Hardwarenamen, RAM-Größe und GPU-Details ab.
        /// </summary>
        /// <returns>
        /// Eine sortierte Liste von Strings mit Hardware-Details.
        /// </returns>
        public async Task<List<string>> GetSystemInformationAsync()
        {
            return await Task.Run(() =>
            {
                var infoList = new List<string>();
                double detectedSpeed = 0;

                // Durchlauf zur Ermittlung der Taktrate
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    var speedSensor = hardware.Sensors.FirstOrDefault(s =>
                        s.SensorType == SensorType.Clock &&
                        (s.Name.Contains("Memory") || s.Name.Contains("Bus")));

                    if (speedSensor?.Value > detectedSpeed)
                        detectedSpeed = speedSensor.Value.Value;
                }

                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();

                    // Filtert virtuelle oder irrelevante Netzwerkadapter aus!
                    if (hardware.HardwareType == HardwareType.Network)
                    {
                        string n = hardware.Name.ToLower();
                        if (n.Contains("kernel") || n.Contains("microsoft") || n.Contains("pseudo") ||
                            n.Contains("qos") || n.Contains("wfp") || n.Contains("lightweight") ||
                            n.Contains("miniport") || n.Contains("filter") || n.Contains("adapter 0") ||
                            n.Contains("*") || n.Contains("virtual") || n.Contains("bluetooth") || n.Contains("isatap"))
                        {
                            continue;
                        }
                    }

                    if (hardware.HardwareType == HardwareType.Memory)
                    {
                        if (hardware.Name.Contains("Virtual")) continue;

                        // Rechnet den genutzen und verfügbaren RAM zusammen
                        double used = hardware.Sensors.FirstOrDefault(s => s.Name == "Memory Used")?.Value ?? 0;
                        double avail = hardware.Sensors.FirstOrDefault(s => s.Name == "Memory Available")?.Value ?? 0;
                        double total = used + avail;

                        if (hardware.Name.Contains("Total"))
                        {
                            infoList.Add($"Arbeitsspeicher Gesamt: {total:F1} GB");
                        }
                        else
                        {
                            string speed = detectedSpeed > 0 ? $"{detectedSpeed:F0} MHz" : "N/V";
                            infoList.Add($"RAM-Modul: {hardware.Name} @ {speed}");
                        }
                    }
                    else if (hardware.HardwareType == HardwareType.GpuNvidia || 
                             hardware.HardwareType == HardwareType.GpuAmd || 
                             hardware.HardwareType == HardwareType.GpuIntel)
                    {
                        var vramSensor = hardware.Sensors.FirstOrDefault(s => s.Name.Contains("Memory Total"));
                        float vram = vramSensor?.Value ?? 0;
                        if (vram > 512) vram /= 1024f;
                        infoList.Add($"GPU: {hardware.Name} ({vram:F0} GB VRAM)");
                    }
                    else
                    {
                        infoList.Add($"{hardware.HardwareType}: {hardware.Name}");
                    }
                }

                // Sortiert die Liste nach Hardware-Priorität
                return infoList.OrderBy(item =>
                {
                    if (item.Contains("Motherboard")) return 0;
                    if (item.Contains("Cpu")) return 1;
                    if (item.Contains("Arbeitsspeicher")) return 2;
                    if (item.Contains("GPU")) return 4;
                    return 5;
                }).ToList();
            });
        }

        /// <summary>
        /// Liest alle aktuell verfügbaren Sensoren der Hardware aus.
        /// </summary>
        /// <returns>
        /// Liste von SensorDataModel-Objekten mit aktuellen Messwerten.
        /// </returns>
        public async Task<List<SensorDataModel>> GetImportantSensorsAsync()
        {
            return await Task.Run(() =>
            {
                var sensorList = new List<SensorDataModel>();

                // Hilfsfunktion, um Hardware und deren SubHardware rekursiv auszulesen
                void ScanHardware(LibreHardwareMonitor.Hardware.IHardware hw)
                {
                    hw.Update();

                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.Value.HasValue)
                        {
                            sensorList.Add(new SensorDataModel(
                                sensor.Name,
                                hw.HardwareType.ToString(),
                                hw.Name,
                                sensor.Value.Value,
                                GetFormatForSensor(sensor.SensorType)
                            ));
                        }
                    }
                    foreach (var subHw in hw.SubHardware)
                    {
                        ScanHardware(subHw);
                    }
                }
                foreach (var hardware in _computer.Hardware)
                {
                    ScanHardware(hardware);
                }

                return sensorList;
            });
        }

        /// <summary>
        /// Weist den verschiedenen Sensortypen ihre physikalischen Einheiten zu.
        /// </summary>
        /// <param name="type">Der Typ des Sensors.</param>
        /// <returns>
        /// Gibt die Einheit als String zurück (z. B. °C, MHz, %).
        /// </returns>
        private static string GetFormatForSensor(SensorType type)
        {
            return type switch
            {
                SensorType.Temperature => "°C",
                SensorType.Clock => "MHz",
                SensorType.Load => "%",
                SensorType.Fan => "%",
                SensorType.Power => "W",
                SensorType.Voltage => "V",
                SensorType.Data => "GB",
                _ => ""
            };
        }
    }
}