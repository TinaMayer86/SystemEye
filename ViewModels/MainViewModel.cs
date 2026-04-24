using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json; // Hinzugefügt für das Laden der Sensor-Config
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SystemEye.Models;
using SystemEye.Services;

namespace SystemEye.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // Konstante für das Speicher-Intervall
        private static readonly int SECOND_COUNTER = 60;

        private readonly HardwareService _hardwareService;
        private readonly DatabaseService _databaseService;
        private readonly ExportService _exportService;
        private readonly ConfigService _configService;
        private readonly ILogger<MainViewModel> _logger;

        private readonly string _sensorSettingPath = Path.Combine("Config", "sensors.json");

        [ObservableProperty]
        private ObservableCollection<string> _systemInformation = new();

        [ObservableProperty]
        private ObservableCollection<SensorDataModel> _currentSensors = new();

        [ObservableProperty]
        private ObservableCollection<SensorConfigModel> _availableSensors = new();

        [ObservableProperty]
        private ObservableCollection<AggregatedSensorData> _databaseRecords = new();

        [ObservableProperty]
        private string _nextUpdateText = $"{SECOND_COUNTER}s";

        private readonly List<SensorDataModel> _minuteBuffer = new();
        private DateTime _lastDbSave = DateTime.Now;
        private int _secondsUntilUpdate = SECOND_COUNTER;

        public event Action? DataUpdated;

        public MainViewModel(
            HardwareService hardwareService,
            DatabaseService databaseService,
            ExportService exportService,
            ConfigService configService,
            ILogger<MainViewModel> logger)
        {
            _hardwareService = hardwareService;
            _databaseService = databaseService;
            _exportService = exportService;
            _configService = configService;
            _logger = logger;

            Task.Run(InitializeAndStartMonitoringAsync);
        }

        private async Task InitializeAndStartMonitoringAsync()
        {
            // Systeminformationen laden
            var info = await _hardwareService.GetSystemInformationAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                SystemInformation.Clear();
                foreach (var line in info) SystemInformation.Add(line);
            });

            // Sensorkonfiguration laden
            await LoadSensorConfigAsync();

            //  Alte Daten aus der DB laden
            await LoadDatabaseDataAsync();

            // Monitoring Timer starten (0.5s Takt)
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(0.5));
            int halfSecondTicks = 0;

            while (await timer.WaitForNextTickAsync())
            {
                await UpdateSensorsAsync();

                // Countdown-Logik
                halfSecondTicks++;
                if (halfSecondTicks >= 2)
                {
                    UpdateCountdown();
                    halfSecondTicks = 0;
                }

                // Prüft ob Intervall für DB-Speicherung erreicht
                if ((DateTime.Now - _lastDbSave).TotalSeconds >= SECOND_COUNTER)
                {
                    await AggregateAndSaveToDatabaseAsync();
                    _lastDbSave = DateTime.Now;
                }
            }
        }

        private void UpdateCountdown()
        {
            _secondsUntilUpdate--;
            if (_secondsUntilUpdate <= 0)
            {
                NextUpdateText = "Speichere...";
            }
            else
            {
                NextUpdateText = $"{_secondsUntilUpdate}s";
            }
        }

        private async Task UpdateSensorsAsync()
        {
            var sensors = await _hardwareService.GetImportantSensorsAsync();

            // Filtern nach aktivierten Sensoren
            var enabledSensors = sensors.Where(s =>
                AvailableSensors.Any(config => config.Name == s.Name && config.IsEnabled)).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentSensors.Clear();
                foreach (var s in enabledSensors)
                {
                    CurrentSensors.Add(s);
                    _minuteBuffer.Add(s);
                }
            });

            DataUpdated?.Invoke();
        }

        private async Task AggregateAndSaveToDatabaseAsync()
        {
            // NaN filtern um SQLite-Fehler zu vermeiden!
            var validData = _minuteBuffer
                .Where(s => !double.IsNaN(s.Value) && !double.IsInfinity(s.Value))
                .ToList();

            if (validData.Count > 0)
            {
                var aggregatedData = new List<AggregatedSensorData>();
                var currentTime = DateTime.Now;
                var groupedSensors = validData.GroupBy(s => s.Name);

                foreach (var group in groupedSensors)
                {
                    var first = group.First();
                    aggregatedData.Add(new AggregatedSensorData
                    {
                        Timestamp = currentTime,
                        Name = group.Key,
                        HardwareType = first.HardwareType,
                        Format = first.Format,
                        MinValue = group.Min(s => (double)s.Value),
                        MaxValue = group.Max(s => (double)s.Value),
                        AvgValue = group.Average(s => (double)s.Value)
                    });
                }

                await _databaseService.SaveAggregatedDataAsync(aggregatedData, "minute_data");
                await LoadDatabaseDataAsync();
            }

            _minuteBuffer.Clear();
            _secondsUntilUpdate = SECOND_COUNTER;
            NextUpdateText = $"{SECOND_COUNTER}s";
        }

        [RelayCommand]
        public async Task LoadDatabaseDataAsync()
        {
            try
            {
                var data = await _databaseService.LoadMinuteDataAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    DatabaseRecords.Clear();
                    foreach (var d in data) DatabaseRecords.Add(d);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Datenbank-Historie.");
            }
        }

        private async Task LoadSensorConfigAsync()
        {
            if (File.Exists(_sensorSettingPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_sensorSettingPath);
                    var config = JsonSerializer.Deserialize<List<SensorConfigModel>>(json);
                    if (config != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AvailableSensors.Clear();
                            foreach (var s in config) AvailableSensors.Add(s);
                        });
                        return;
                    }
                }
                catch (Exception ex) { _logger.LogError(ex, "Fehler beim Laden der Sensorkonfiguration."); }
            }

            // Fallback
            var sensors = await _hardwareService.GetImportantSensorsAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableSensors.Clear();
                foreach (var s in sensors)
                {
                    if (!AvailableSensors.Any(x => x.Name == s.Name))
                        AvailableSensors.Add(new SensorConfigModel { Name = s.Name, HardwareType = s.HardwareType, IsEnabled = true });
                }
            });
        }

        [RelayCommand]
        private async Task ExportLogAsync()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = Path.Combine(desktopPath, $"{DateTime.Now:ddMMyyyy_HHmmss}.txt");
                await _exportService.ExportCurrentDataToTxtAsync(filePath, CurrentSensors.ToList());
                MessageBox.Show($"Log-Datei gespeichert:\n{filePath}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Export fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}