using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemEye.Models
{
    /// <summary>
    /// Repräsentiert die Konfigurationseinstellungen für einen einzelnen Sensor.
    /// Erbt von ObservableObject für die Datenbindung in der UI.
    /// </summary>
    public partial class SensorConfigModel : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string HardwareType { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isEnabled = true; // Alle Sensoren sind standardmäßig an
    }
}
