using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;
using SystemEye.Services;

namespace SystemEye.ViewModels
{
    /// <summary>
    /// ViewModel für den Informations-Tab.
    /// Lädt und hält die allgemeinen Hardware-Informationen des Systems.
    /// </summary>
    public partial class InfoViewModel : ObservableObject
    {
        private readonly HardwareService _hardwareService;
        private readonly ILogger<InfoViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<string> _systemInformation = new();

        public InfoViewModel(HardwareService hardwareService, ILogger<InfoViewModel> logger)
        {
            _hardwareService = hardwareService;
            _logger = logger;
        }

        /// <summary>
        /// Lädt die grundlegenden Systeminformationen asynchron über den HardwareService.
        /// </summary>
        public async Task LoadSystemInformationAsync()
        {
            try
            {
                var info = await _hardwareService.GetSystemInformationAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SystemInformation.Clear();
                    foreach (var line in info)
                    {
                        SystemInformation.Add(line);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Systeminformationen.");
            }
        }
    }
}