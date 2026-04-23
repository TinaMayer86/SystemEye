using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemEye.Models
{
    public partial class SensorConfigModel : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string HardwareType { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isEnabled = true; // Alle Sensoren sind standardmäßig an
    }
}
