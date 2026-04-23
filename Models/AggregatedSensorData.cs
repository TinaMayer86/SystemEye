namespace SystemEye.Models
{
    /// <summary>
    /// Repräsentiert aggregierte Sensordaten für einen 
    /// bestimmten Zeitpunkt, einschließlich 
    /// Minimal‑, Maximal‑ und Durchschnittswert sowie 
    /// zugehörigem Sensor‑ und Hardwaretyp.
    /// </summary>
    public class AggregatedSensorData
    {
        public DateTime Timestamp { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HardwareType { get; set; } = string.Empty;
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double AvgValue { get; set; }
        public string Format { get; set; } = string.Empty;
    }
}
