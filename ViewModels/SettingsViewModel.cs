using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using SystemEye.Models;
using SystemEye.Services;

namespace SystemEye.ViewModels
{
    /// <summary>
    /// ViewModel für den Einstellungen-Tab.
    /// Verwaltet die Sensorkonfiguration, API-Steuerung, Datenbank-Resets und Exporte.
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly ExportService _exportService;
        private readonly ApiService _apiService;
        private readonly HardwareService _hardwareService;
        private readonly ILogger<SettingsViewModel> _logger;

        private readonly string _sensorSettingPath = Path.Combine("Config", "sensors.json");
        private bool _isBulkUpdating = false;

        public string ApiNetwork => $"http://{GetLocalIPAddress()}:5000/swagger";

        // Event, um dem MainViewModel mitzuteilen, dass sich die Konfiguration geändert hat
        public event Action? ConfigChanged;

        [ObservableProperty]
        private ObservableCollection<SensorConfigModel> _availableSensors = new();

        [ObservableProperty]
        private bool _isApiActive;

        [ObservableProperty]
        private string _apiStatusText = "API Starten";

        [ObservableProperty]
        private string _searchText = "";

        public SettingsViewModel(
            DatabaseService databaseService,
            ExportService exportService,
            ApiService apiService,
            HardwareService hardwareService,
            ILogger<SettingsViewModel> logger)
        {
            _databaseService = databaseService;
            _exportService = exportService;
            _apiService = apiService;
            _hardwareService = hardwareService;
            _logger = logger;
        }

        /// <summary>
        /// Ermittelt die lokale IPv4‑Adresse des Systems, indem alle verfügbaren
        /// Netzwerkadapter durchsucht werden. Gibt die erste gefundene IPv4‑Adresse
        /// zurück oder löst eine Ausnahme aus, wenn keine entsprechende Adresse
        /// vorhanden ist.
        /// </summary>
        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        /// <summary>
        /// Lädt die Liste der verfügbaren Sensoren aus der sensors.json oder initialisiert diese neu.
        /// </summary>
        [RelayCommand]
        public async Task LoadSensorConfigAsync()
        {
            bool fileLoaded = false;

            if (File.Exists(_sensorSettingPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_sensorSettingPath);
                    var config = JsonSerializer.Deserialize<System.Collections.Generic.List<SensorConfigModel>>(json);

                    if (config != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AvailableSensors.Clear();
                            foreach (var s in config)
                            {
                                s.PropertyChanged += async (sender, e) =>
                                {
                                    if (!_isBulkUpdating && e.PropertyName == nameof(SensorConfigModel.IsEnabled))
                                    {
                                        await SaveSensorConfigAsync();
                                        ConfigChanged?.Invoke(); // Benachrichtigt das System über die Änderung
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

            var currentHardwareSensors = await _hardwareService.GetImportantSensorsAsync();
            bool newSensorsAdded = false;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!fileLoaded) AvailableSensors.Clear();

                foreach (var s in currentHardwareSensors)
                {
                    if (!AvailableSensors.Any(x => x.Name == s.Name && x.HardwareType == s.HardwareType))
                    {
                        var newSensor = new SensorConfigModel
                        {
                            Name = s.Name,
                            HardwareType = s.HardwareType,
                            IsEnabled = !fileLoaded
                        };

                        newSensor.PropertyChanged += async (sender, e) =>
                        {
                            if (e.PropertyName == nameof(SensorConfigModel.IsEnabled))
                            {
                                await SaveSensorConfigAsync();
                                ConfigChanged?.Invoke();
                            }
                        };
                        AvailableSensors.Add(newSensor);
                        newSensorsAdded = true;
                    }
                }
            });

            if (newSensorsAdded || !fileLoaded)
            {
                await SaveSensorConfigAsync();
            }
        }

        /// <summary>
        /// Aktualisiert die Filterung der verfügbaren Sensoren basierend auf dem
        /// eingegebenen Suchtext. Leert den Filter bei leerer Eingabe oder zeigt
        /// nur jene Sensoren an, deren Name oder Hardwaretyp den Suchbegriff enthält.
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(AvailableSensors);

            if (string.IsNullOrWhiteSpace(value))
            {
                view.Filter = null;
            }
            else
            {
                var searchLower = value.ToLower();
                view.Filter = item =>
                {
                    if (item is SensorConfigModel sensor)
                    {
                        return (sensor.Name != null && sensor.Name.ToLower().Contains(searchLower)) ||
                               (sensor.HardwareType != null && sensor.HardwareType.ToLower().Contains(searchLower));
                    }
                    return false;
                };
            }
        }

        /// <summary>
        /// Speichert die aktuelle Sensorkonfiguration als formatiertes JSON‑Dokument.
        /// Erstellt bei Bedarf das Zielverzeichnis und protokolliert sowohl erfolgreiche
        /// als auch fehlerhafte Speicher­vorgänge.
        /// </summary>
        /// <returns>
        /// Ein Task, das den asynchronen Speichervorgang der Sensorkonfiguration repräsentiert.
        /// </returns>
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

        /// <summary>
        /// Exportiert die in der letzten Stunde aufgezeichneten Sensordaten als
        /// TXT‑Datei auf den Desktop. Prüft zunächst, ob Daten vorhanden sind,
        /// und informiert den Benutzer über Erfolg oder Fehler des Exportvorgangs.
        /// </summary>
        /// <returns>
        /// Ein Task, das den asynchronen Exportvorgang repräsentiert.
        /// </returns>
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
        /// Setzt die Datenbank zurück, indem alle gespeicherten Historiendaten gelöscht
        /// werden. Fordert den Benutzer zuvor zur Bestätigung auf und informiert über
        /// Erfolg oder Fehler des Vorgangs.
        /// </summary>
        /// <returns>
        /// Ein Task, das den asynchronen Löschvorgang der Datenbank repräsentiert.
        /// </returns>
        [RelayCommand]
        public async Task ResetDatabaseAsync()
        {
            var result = MessageBox.Show(
                "Möchten Sie wirklich alle gespeicherten Historiendaten löschen?",
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

        /// <summary>
        /// Schaltet die interne REST‑API ein oder aus, abhängig vom aktuellen Status.
        /// Startet die API auf dem Standardport oder beendet sie kontrolliert und
        /// aktualisiert anschließend die zugehörigen UI‑Statusanzeigen.
        /// </summary>
        /// <returns>
        /// Ein Task, das den asynchronen Umschaltvorgang der API repräsentiert.
        /// </returns>
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

        /// <summary>
        /// Aktiviert alle verfügbaren Sensoren in der Konfiguration und speichert die
        /// aktualisierten Einstellungen. Unterdrückt während des Massenupdates
        /// Änderungsereignisse und löst anschließend eine zentrale Konfigurationsänderung aus.
        /// </summary>
        /// <returns>
        /// Ein Task, das den asynchronen Aktualisierungs‑ und Speicherprozess repräsentiert.
        /// </returns>
        [RelayCommand]
        public async Task EnableAllSensorsAsync()
        {
            _isBulkUpdating = true;
            foreach (var sensor in AvailableSensors)
            {
                sensor.IsEnabled = true;
            }
            _isBulkUpdating = false;

            await SaveSensorConfigAsync();
            ConfigChanged?.Invoke();
        }

        /// <summary>
        /// Deaktiviert alle verfügbaren Sensoren in der Konfiguration und speichert die
        /// aktualisierten Einstellungen. Unterdrückt während des Massenupdates
        /// Änderungsereignisse und löst anschließend eine zentrale Konfigurationsänderung aus.
        /// </summary>
        /// <returns>
        /// Ein Task, das den asynchronen Aktualisierungs‑ und Speicherprozess repräsentiert.
        /// </returns>
        [RelayCommand]
        public async Task DisableAllSensorsAsync()
        {
            _isBulkUpdating = true;
            foreach (var sensor in AvailableSensors)
            {
                sensor.IsEnabled = false;
            }
            _isBulkUpdating = false;

            await SaveSensorConfigAsync();
            ConfigChanged?.Invoke();
        }
    }
}