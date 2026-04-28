using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SystemEye.Models;
using SystemEye.Services;

namespace SystemEye.ViewModels
{
    /// <summary>
    /// Haupt-ViewModel der Anwendung
    /// Steuert den Monitoring-Zyklus, die Datenaggregation und die Interaktion zwischen UI und Services
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private static readonly int SECOND_COUNTER = 60; //Intervall der aktualisierung ( bei änderungen halfSecondTicks anpassen!)
        private const int PageSize = 100;

        private readonly HardwareService _hardwareService;
        private readonly DatabaseService _databaseService;
        private readonly ExportService _exportService;
        private readonly ConfigService _configService;
        private readonly ApiService _apiService;
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

        [ObservableProperty]
        private DateTime _lastDatabaseUpdate = DateTime.Now;

        [ObservableProperty]
        private bool _isApiActive;

        [ObservableProperty]
        private string _apiStatusText = "API Starten";

        // Interne Buffer + Zustände
        private readonly List<SensorDataModel> _minuteBuffer = new();
        private DateTime _lastDbSave = DateTime.Now;
        private int _secondsUntilUpdate = SECOND_COUNTER;
        private int _currentHistoryOffset = 0;

        public event Action? DataUpdated;

        public MainViewModel(
            HardwareService hardwareService,
            DatabaseService databaseService,
            ExportService exportService,
            ConfigService configService,
            ApiService apiService,
            ILogger<MainViewModel> logger)
        {
            _hardwareService = hardwareService;
            _databaseService = databaseService;
            _exportService = exportService;
            _configService = configService;
            _apiService = apiService;
            _logger = logger;

            Task.Run(InitializeAndStartMonitoringAsync); // Startet den Hintergrund-Task für das Monitoring
        }

        /// <summary>
        /// Initialisiert die Sensorkonfiguration und startet die Endlosschleife zur Datenabfrage
        /// </summary>
        private async Task InitializeAndStartMonitoringAsync()
        {
            var info = await _hardwareService.GetSystemInformationAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                SystemInformation.Clear();
                foreach (var line in info) SystemInformation.Add(line);
            });

            await LoadSensorConfigAsync();
            await LoadDatabaseDataAsync();

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(0.5));
            int halfSecondTicks = 0;

            while (await timer.WaitForNextTickAsync())
            {
                await UpdateSensorsAsync();

                halfSecondTicks++;
                if (halfSecondTicks >= 2)
                {
                    UpdateCountdown();
                    halfSecondTicks = 0;
                }
                // Prüft, ob das Speicherintervall erreicht wurde
                if ((DateTime.Now - _lastDbSave).TotalSeconds >= SECOND_COUNTER)
                {
                    await AggregateAndSaveToDatabaseAsync();
                    _lastDbSave = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Aktualisiert den Countdown-Text in der UI
        /// </summary>
        private void UpdateCountdown()
        {
            _secondsUntilUpdate--;
            if (_secondsUntilUpdate <= 0)
            {
                NextUpdateText = "Sync...";
            }
            else
            {
                NextUpdateText = $"{_secondsUntilUpdate}s";
            }
        }

        /// <summary>
        /// Fragt aktuelle Sensordaten ab und filtert diese nach der Benutzerkonfiguration
        /// </summary>
        private async Task UpdateSensorsAsync()
        {
            var sensors = await _hardwareService.GetImportantSensorsAsync();
            var enabledSensors = sensors.Where(s =>
                AvailableSensors.Any(config => config.Name == s.Name && config.IsEnabled)).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentSensors.Clear();
                foreach (var s in enabledSensors)
                {
                    CurrentSensors.Add(s);
                    _minuteBuffer.Add(s); // Sammelt Daten für die spätere Aggregation
                }
            });

            DataUpdated?.Invoke();
        }

        /// <summary>
        /// Aggregiert die gesammelten Daten des Puffers (Min/Max/Avg) und speichert sie in der Datenbank.
        /// </summary>
        private async Task AggregateAndSaveToDatabaseAsync()
        {
            // NaN und Infinity filtern, um Datenbankfehler zu vermeiden 
            var validData = _minuteBuffer
                .Where(s => !double.IsNaN(s.Value) && !double.IsInfinity(s.Value))
                .ToList();

            if (validData.Count > 0)
            {
                var aggregatedData = new List<AggregatedSensorData>();
                var currentTime = DateTime.Now;
                var groupedSensors = validData.GroupBy(s => new { s.Name, s.HardwareType });

                foreach (var group in groupedSensors)
                {
                    var first = group.First();
                    aggregatedData.Add(new AggregatedSensorData
                    {
                        Timestamp = currentTime,
                        Name = group.Key.Name,
                        HardwareType = group.Key.HardwareType,
                        Format = first.Format,
                        MinValue = group.Min(s => (double)s.Value),
                        MaxValue = group.Max(s => (double)s.Value),
                        AvgValue = group.Average(s => (double)s.Value)
                    });
                }

                try
                {
                    // Speichern der aggregierten Daten in der Datenbank 
                    await _databaseService.SaveAggregatedDataAsync(aggregatedData, "minute_data");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        LastDatabaseUpdate = currentTime;
                    });

                    if (_currentHistoryOffset == 0)
                    {
                        await LoadDatabaseDataAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Aggregieren und Speichern der Sensordaten.");
                }
            }

            // Buffer leeren und Timer für die nächste Minute zurücksetzen 
            _minuteBuffer.Clear();
            _secondsUntilUpdate = SECOND_COUNTER;
            NextUpdateText = $"{SECOND_COUNTER}s";
        }

        /// <summary>
        /// Lädt die Historie aus der Datenbank unter Berücksichtigung des aktuellen Offsets.
        /// </summary>
        [RelayCommand]
        public async Task LoadDatabaseDataAsync()
        {
            try
            {
                var data = await _databaseService.LoadMinuteDataAsync(_currentHistoryOffset, PageSize);
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

        /// <summary>
        /// Lädt die Liste der verfügbaren Sensoren aus der sensors.json oder initialisiert diese neu.
        /// </summary>
        [RelayCommand]
        public async Task NextHistoryPageAsync()
        {
            _currentHistoryOffset += PageSize;
            await LoadDatabaseDataAsync();
        }

        [RelayCommand]
        public async Task PreviousHistoryPageAsync()
        {
            if (_currentHistoryOffset >= PageSize)
            {
                _currentHistoryOffset -= PageSize;
                await LoadDatabaseDataAsync();
            }
        }

        private async Task LoadSensorConfigAsync()
        {
            bool fileLoaded = false;

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
                            foreach (var s in config)
                            {
                                s.PropertyChanged += async (sender, e) =>
                                {
                                    if (e.PropertyName == nameof(SensorConfigModel.IsEnabled))
                                    {
                                        await SaveSensorConfigAsync();
                                        await UpdateSensorsAsync();
                                    }
                                };
                                AvailableSensors.Add(s);
                            }
                        });
                        fileLoaded = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Laden der sensors.json.");
                }
            }
            // Falls keine Datei existiert, Sensoren von Hardware-Service beziehen!
            if (!fileLoaded)
            {
                var sensors = await _hardwareService.GetImportantSensorsAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    AvailableSensors.Clear();
                    foreach (var s in sensors)
                    {
                        if (!AvailableSensors.Any(x => x.Name == s.Name && x.HardwareType == s.HardwareType))
                        {
                            var newSensor = new SensorConfigModel
                            {
                                Name = s.Name,
                                HardwareType = s.HardwareType,
                                IsEnabled = true
                            };

                            newSensor.PropertyChanged += async (sender, e) =>
                            {
                                if (e.PropertyName == nameof(SensorConfigModel.IsEnabled))
                                {
                                    await SaveSensorConfigAsync();
                                    await UpdateSensorsAsync();
                                }
                            };
                            AvailableSensors.Add(newSensor);
                        }
                    }
                });
                await SaveSensorConfigAsync();
            }
        }

        /// <summary>
        /// Exportiert die Daten der letzten Stunde auf den Desktop des Benutzers.
        /// </summary>
        [RelayCommand]
        private async Task ExportLogAsync()
        {
            try
            {
                var hourData = await _databaseService.LoadLastHourDataAsync();

                if (!hourData.Any())
                {
                    MessageBox.Show("Keine Daten für die letzte Stunde in der Datenbank vorhanden.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = Path.Combine(desktopPath, $"SystemEye_StundenExport_{DateTime.Now:HHmmss}.txt");

                await _exportService.ExportDatabaseDataToTxtAsync(filePath, hourData);

                MessageBox.Show($"Export der letzten Stunde erfolgreich:\n{filePath}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Export fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Speichert die aktuelle Sensorauswahl in die sensors.json.
        /// </summary>
        [RelayCommand]
        public async Task SaveSensorConfigAsync()
        {
            try
            {
                var directory = Path.GetDirectoryName(_sensorSettingPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(AvailableSensors, options);

                await File.WriteAllTextAsync(_sensorSettingPath, json);

                _logger.LogInformation("Sensorkonfiguration wurde gespeichert.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Speichern der Sensorkonfiguration.");
                MessageBox.Show("Fehler beim Speichern der Einstellungen.");
            }
        }

        [RelayCommand]
        public async Task ResetDatabaseAsync()
        {
            var result = MessageBox.Show(
                "Möchten Sie wirklich alle gespeicherten Historiendaten löschen? Dies kann nicht rückgängig gemacht werden.",
                "Datenbank zurücksetzen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _databaseService.ClearDatabaseAsync();
                    MessageBox.Show("Datenbank wurde erfolgreich zurückgesetzt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Zurücksetzen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public async Task ToggleApiAsync()
        {
            if (!IsApiActive)
            {
                await _apiService.StartApiAsync(5000);
                ApiStatusText = "API Stoppen";
                IsApiActive = true;
            }
            else
            {
                await _apiService.StopApiAsync();
                ApiStatusText = "API Starten";
                IsApiActive = false;
            }
        }
    }
}