namespace SystemEye.Models
{
    public record SensorDataModel(
        string Name,
        string HardwareType,
        string SensorType,
        float Value,
        string Format
    );
}
