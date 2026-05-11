using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Windows;
using SystemEye.Models;
using SystemEye.Services;

namespace SystemEye.ViewModels
{
    /// <summary>
    /// Das zentrale MainViewModel (Host).
    /// Hält die Child-ViewModels für die UI und steuert den asynchronen Background-Loop
    /// zur Hardware-Abfrage und Datenbank-Aggregation.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private static readonly int SecondCounter = 60; // Intervall der Aktualisierung in Sekunden

        private readonly HardwareService _hardwareService;
        private readonly DatabaseService _databaseService;
        private readonly ILogger<MainViewModel> _logger;


        // Child ViewModels (werden ans UI gebunden)
        public InfoViewModel InfoVM { get; }
        public LiveViewModel LiveVM { get; }
        public HistoryViewModel HistoryVM { get; }
        public SettingsViewModel SettingsVM { get; }

        // Interne Buffer + Zustände für den Background-Loop
        private readonly List<SensorDataModel> _minuteBuffer = new();
        private readonly List<SensorDataModel> _activeSensors = new();
        private DateTime _lastDbSave = DateTime.Now;
        private int _secondsUntilUpdate = SecondCounter;

        public MainViewModel(
            InfoViewModel infoVM,
            LiveViewModel liveVM,
            HistoryViewModel historyVM,
            SettingsViewModel settingsVM,
            HardwareService hardwareService,
            DatabaseService databaseService,
            ILogger<MainViewModel> logger)
        {
            InfoVM = infoVM;
            LiveVM = liveVM;
            HistoryVM = historyVM;
            SettingsVM = settingsVM;

            _hardwareService = hardwareService;
            _databaseService = databaseService;
            _logger = logger;

            SettingsVM.ConfigChanged += OnSettingsConfigChanged;

            Task.Run(InitializeAndStartMonitoringAsync);
        }

        /// <summary>
        /// Reagiert auf Änderungen der Sensorkonfiguration und aktualisiert die
        /// Live‑Sensordaten asynchron, sodass die Anzeige sofort an die neuen
        /// Einstellungen angepasst wird.
        /// </summary>
        private async void OnSettingsConfigChanged()
        {
            await UpdateSensorsAsync();
        }

        /// <summary>
        /// Initialisiert alle ViewModels und startet die Endlosschleife zur Datenabfrage
        /// </summary>
        private async Task InitializeAndStartMonitoringAsync()
        {
            // Initiale Daten in den Child-ViewModels laden
            await InfoVM.LoadSystemInformationAsync();
            await SettingsVM.LoadSensorConfigAsync();
            await HistoryVM.LoadDatabaseDataAsync();

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1)); // Aktualisierungszeit

            while (await timer.WaitForNextTickAsync())
            {
                await UpdateSensorsAsync();

                // Prüft, ob das Speicherintervall für die Datenbank erreicht wurde
                if ((DateTime.Now - _lastDbSave).TotalSeconds >= SecondCounter)
                {
                    await AggregateAndSaveToDatabaseAsync();
                    _lastDbSave = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Aktualisiert den Countdown-Text im Historie-ViewModel
        /// </summary>
        private void UpdateCountdown()
        {
            _secondsUntilUpdate--;
            if (_secondsUntilUpdate <= 0)
            {
                HistoryVM.NextUpdateText = "Sync...";
            }
            else
            {
                HistoryVM.NextUpdateText = $"{_secondsUntilUpdate}s";
            }
        }

        /// <summary>
        /// Fragt aktuelle Sensordaten ab, filtert diese nach der Benutzerkonfiguration 
        /// und übergibt sie an das LiveViewModel für die UI-Darstellung.
        /// </summary>
        private async Task UpdateSensorsAsync()
        {
            if (Application.Current == null) return;        // Sicherheits-Check! Wenn schon am schließen dann Abbrechen

            var sensors = await _hardwareService.GetImportantSensorsAsync();

            _activeSensors.Clear(); // Müllvermeidung, list wird geleert anstatt sie neu zumachen

            foreach (var s in sensors)
            {
                foreach (var config in SettingsVM.AvailableSensors)
                {
                    if (config.Name == s.Name && config.IsEnabled)
                    {
                        _activeSensors.Add(s);
                        break;
                    }
                }
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                foreach (var s in _activeSensors)
                {
                    _minuteBuffer.Add(s);
                }
            });

            LiveVM.UpdateSensorData(_activeSensors);
        }

        /// <summary>
        /// Aggregiert die gesammelten Daten des Puffers (Min/Max/Avg) und speichert sie in der Datenbank.
        /// </summary>
        private async Task AggregateAndSaveToDatabaseAsync()
        {
            // NaN und Infinity filtern, um Datenbankfehler zu vermeiden 
            var validData = _minuteBuffer
                .Where(s => !double.IsNaN(s.Value) && !double.IsInfinity(s.Value)).ToList();

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

                    // Tabelle im History-Tab direkt aktualisieren, damit die neuesten Daten sichtbar sind
                    await HistoryVM.LoadDatabaseDataAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Aggregieren und Speichern der Sensordaten.");
                }
            }

            // Buffer leeren und Timer für die nächste Minute zurücksetzen 
            _minuteBuffer.Clear();
            _secondsUntilUpdate = SecondCounter;
            HistoryVM.NextUpdateText = $"{SecondCounter}s";
        }
    }
}