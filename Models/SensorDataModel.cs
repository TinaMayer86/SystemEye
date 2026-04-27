namespace SystemEye.Models
{
    /// <summary>
    /// Repräsentiert einen einzelnen Datenpunkt eines Sensors.
    /// Nutzt den Record-Typ für eine unveränderliche Datenspeicherung.
    /// </summary>
    /// <param name="Name">Bezeichnung des Sensors.</param>
    /// <param name="HardwareType">Typ der Hardware (z. B. CPU, GPU).</param>
    /// <param name="SensorType">Art des Sensors (z. B. Temperature, Load).</param>
    /// <param name="Value">Aktueller Messwert.</param>
    /// <param name="Format">Einheit oder Formatierung des Wertes.</param>
    public record SensorDataModel(
        string Name,
        string HardwareType,
        string SensorType,
        float Value,
        string Format
    );
}
