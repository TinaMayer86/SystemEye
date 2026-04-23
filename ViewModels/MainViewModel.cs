using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using SystemEye.Models;
using SystemEye.Services;

namespace SystemEye.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly HardwareService _hardwareService;
        private readonly DatabaseService _databaseService;
        private readonly ExportService _exportService;
        private readonly ILogger<MainViewModel> _logger;

        private readonly string _sensorSettingPath = Path.Combine("Config", "sensors.json");

        [ObservableProperty]
        private ObservableCollection<string> _systemInformation = new();

        [ObservableProperty]
        private ObservableCollection<SensorDataModel> _currentSensors = new();

        [ObservableProperty]
        private ObservableCollection<SensorConfigModel> _availableSensors = new();

        [ObservableProperty]
        private ObservableCollection<AggregatedSensorData> _aggregatedSensorDatas = new();

        private readonly List<SensorDataModel> _minuteBuffer = new();
        private DateTime _lastDbSave = DateTime.Now;

        public event Action? DataUpdated;

        public MainViewModel(
            HardwareService hardwareService,
            DatabaseService databaseService,
            ExportService exportService,
            ILogger<MainViewModel> logger)
        {
            _hardwareService = hardwareService;
            _databaseService = databaseService;
            _exportService = exportService;
            _logger = logger;

            _ = InitializeAndStartMonitoringAsync();
        }

        private async Task InitializeAndStartMonitoringAsync()
        {
            var sysInfo = await _hardwareService.GetSystemInformationAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var info in sysInfo)
                {
                    SystemInformation.Add(info);
                }
            });

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync())
            {
                try
                {
                    await UpdateSensorsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kritischer Fehler im Hintergrund-Timer (Sensor-Update).");
                }
            }
        }

        private async Task UpdateSensorsAsync()
        {
            var rawSensors = await _hardwareService.GetImportantSensorsAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (AvailableSensors.Count == 0)
                {
                    LoadSensorSettings(rawSensors);
                }

                var activeSensorNames = AvailableSensors.Where(s => s.IsEnabled).Select(s => s.Name).ToHashSet();
                var filteredSensors = rawSensors.Where(s => activeSensorNames.Contains(s.Name)).ToList();

                CurrentSensors.Clear();
                foreach (var sensor in filteredSensors)
                {
                    CurrentSensors.Add(sensor);
                }

                DataUpdated?.Invoke();
            });

            _minuteBuffer.AddRange(rawSensors);

            if ((DateTime.Now - _lastDbSave).TotalSeconds >= 60)
            {
                await AggregateAndSaveToDatabaseAsync();
                _lastDbSave = DateTime.Now;
            }
        }

        private void LoadSensorSettings(List<SensorDataModel> rawSensors)
        {
            List<string> savedActiveSensors = new();

            if (File.Exists(_sensorSettingPath))
            {
                try
                {
                    var sensorJson = File.ReadAllText(_sensorSettingPath);
                    savedActiveSensors = JsonSerializer.Deserialize<List<string>>(sensorJson) ?? new();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Konnte sensors.json nicht laden. Es werden alle Sensoren aktiviert.");
                }
            }
        }

        private async Task AggregateAndSaveToDatabaseAsync()
        {
            if (_minuteBuffer.Count == 0) return;
            var aggregatedData = new List<AggregatedSensorData>();
            var currentTime = DateTime.Now;
            var groupedSensors = _minuteBuffer.GroupBy(s => s.Name);

            foreach (var group in groupedSensors)
            {
                var first = group.First();
                aggregatedData.Add(new AggregatedSensorData
                {
                    Timestamp = currentTime,
                    Name = group.Key,
                    HardwareType = first.HardwareType,
                    Format = first.Format,
                    MinValue = group.Min(s => s.Value),
                    MaxValue = group.Max(s => s.Value),
                    AvgValue = group.Average(s => s.Value)
                });
            }

            await _databaseService.SaveAggregatedDataAsync(aggregatedData, "MinuteData");
            _minuteBuffer.Clear();

            if (AggregatedSensorDatas.Count > 0) await LoadDatabaseDataAsync();
        }

        [RelayCommand]
        private async Task LoadDatabaseDataAsync()
        {
            try
            {
                var data = await _databaseService.LoadMinuteDataAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AggregatedSensorDatas.Clear();
                    foreach (var d in data)
                    {
                        AggregatedSensorDatas.Add(d);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Es gab ein Problem mit der Datenbank!\nBitte prüfe, ob dein MySQL-Server läuft.\n\nTechnischer Fehler: {ex.Message}",
                    "Datenbankfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ExportLogAsync()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = System.IO.Path.Combine(desktopPath, $"{DateTime.Now:ddMMyyyy_HHmmss}.txt");

                await _exportService.ExportCurrentDataToTxtAsync(filePath, CurrentSensors.ToList());

                MessageBox.Show($"Log-Datei erfolgreich gespeichert:\n{filePath}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Exportieren der TXT-Datei.");
                MessageBox.Show($"Die Datei konnte nicht gespeichert werden!\nFehler: {ex.Message}",
                    "Export fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}