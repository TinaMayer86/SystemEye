using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using SystemEye.Models;

namespace SystemEye.Services
{
    public class HardwareService : IDisposable
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
                IsControllerEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true
            };

            try
            {
                _computer.Open();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Kritischer Fehler beim Starten des LibreHardwareMonitors. Sensoren können nicht gelesen werden.");
            }
        }

        public async Task<List<string>> GetSystemInformationAsync()
        {
            return await Task.Run(() =>
            {
                var infoList = new List<string>();
                foreach (var hardware in _computer.Hardware)
                {
                    try
                    {
                        hardware.Update();
                        infoList.Add($"{hardware.HardwareType}: {hardware.Name}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Hardware-Info für {HardwareName} konnte nicht gelesen werden.", hardware.Name);
                    }
                }
                return infoList;
            });
        }

        public async Task<List<SensorDataModel>> GetImportantSensorsAsync()
        {
            return await Task.Run(() =>
            {
                var sensors = new List<SensorDataModel>();
                foreach (var hardware in _computer.Hardware)
                {
                    try
                    {
                        hardware.Update();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Sensor-Update für {HardwareName} fehlgeschlagen. Hardware wird für diese Sekunde übersprungen.", hardware.Name);
                        continue;
                    }

                    foreach (var sensor in hardware.Sensors)
                    {
                        bool isImportant = true;
                        string format = "";

                        if (sensor.SensorType == SensorType.Load && (sensor.Name.Contains("Total") || sensor.Name.Contains("Core")))
                        {
                            isImportant = true;
                            format = "%";
                        }
                        else if (sensor.SensorType == SensorType.Temperature)
                        {
                            isImportant = true;
                            format = "°C";
                        }
                        else if (sensor.SensorType == SensorType.Fan)
                        {
                            isImportant = true;
                            format = "RPM";
                        }
                        else if (sensor.SensorType == SensorType.Data || sensor.SensorType == SensorType.SmallData)
                        {
                            isImportant = true;
                            format = "GB";
                        }

                        if (isImportant && sensor.Value.HasValue && !float.IsNaN(sensor.Value.Value))
                        {
                            sensors.Add(new SensorDataModel(
                                sensor.Name,
                                hardware.HardwareType.ToString(),
                                sensor.SensorType.ToString(),
                                sensor.Value.Value,
                                format
                            ));
                        }
                    }
                }
                return sensors;
            });
        }

        public void Dispose()
        {
            try
            {
                _computer.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim ordnungsgemäßen Schließen des Hardware-Monitors.");
            }
        }
    }
}
