using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;
using SystemEye.Models;
using SystemEye.Services;

namespace SystemEye.ViewModels
{
    /// <summary>
    /// ViewModel für den Historie-Tab.
    /// Verwaltet das Laden, Anzeigen und Blättern der historischen Sensordaten aus der Datenbank.
    /// </summary>
    public partial class HistoryViewModel : ObservableObject
    {
        private const int PageSize = 100;
        private int _currentHistoryOffset = 0;

        private readonly DatabaseService _databaseService;
        private readonly ILogger<HistoryViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<AggregatedSensorData> _databaseRecords = new();

        [ObservableProperty]
        private string _nextUpdateText = "60s";

        public HistoryViewModel(DatabaseService databaseService, ILogger<HistoryViewModel> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
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
        /// Lädt die nächste Seite (ältere Daten) aus der Datenbank.
        /// </summary>
        [RelayCommand]
        public async Task NextHistoryPageAsync()
        {
            _currentHistoryOffset += PageSize;
            await LoadDatabaseDataAsync();
        }

        /// <summary>
        /// Lädt die vorherige Seite (neuere Daten) aus der Datenbank.
        /// </summary>
        [RelayCommand]
        public async Task PreviousHistoryPageAsync()
        {
            if (_currentHistoryOffset >= PageSize)
            {
                _currentHistoryOffset -= PageSize;
                await LoadDatabaseDataAsync();
            }
        }
    }
}