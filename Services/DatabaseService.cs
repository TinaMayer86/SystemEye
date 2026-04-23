using Microsoft.Extensions.Logging;
using MySqlConnector;
using SystemEye.Models;

namespace SystemEye.Services
{
    public class DatabaseService
    {
        private readonly DatabaseConfig _config;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(DatabaseConfig config, ILogger<DatabaseService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SaveAggregatedDataAsync(List<AggregatedSensorData> datalist, string tableName)
        {
            if (datalist.Count == 0) return;

            try
            {
                using var connection = new MySqlConnection(_config.GetConnectionString());
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = $"INSERT INTO {tableName} (timestamp, name, hardware_type, min_value, max_value, avg_value, format) VALUES (@Timestamp, @name, @hardware_type, @min_value, @max_value, @avg_value, @format)";

                var pTime = cmd.Parameters.Add("@Timestamp", MySqlDbType.DateTime);
                var pName = cmd.Parameters.Add("@name", MySqlDbType.VarChar);
                var pHardwareType = cmd.Parameters.Add("@hardware_type", MySqlDbType.VarChar);
                var pMinValue = cmd.Parameters.Add("@min_value", MySqlDbType.Double);
                var pMaxValue = cmd.Parameters.Add("@max_value", MySqlDbType.Double);
                var pAvgValue = cmd.Parameters.Add("@avg_value", MySqlDbType.Double);
                var pFormat = cmd.Parameters.Add("@format", MySqlDbType.VarChar);

                foreach (var data in datalist)
                {
                    pTime.Value = data.Timestamp;
                    pName.Value = data.Name;
                    pHardwareType.Value = data.HardwareType;
                    pMinValue.Value = data.MinValue;
                    pMaxValue.Value = data.MaxValue;
                    pAvgValue.Value = data.AvgValue;
                    pFormat.Value = data.Format;

                    await cmd.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Speichern der Live-Daten in die Tabelle {TableName}.", tableName);
            }
        }

        public async Task<List<AggregatedSensorData>> LoadMinuteDataAsync()
        {
            var list = new List<AggregatedSensorData>();

            try
            {
                using var connection = new MySqlConnection(_config.GetConnectionString());
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM minute_data ORDER BY timestamp DESC LIMIT 1000;";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new AggregatedSensorData
                    {
                        Timestamp = reader.GetDateTime("timestamp"),
                        Name = reader.GetString("name"),
                        HardwareType = reader.GetString("hardware_type"),
                        MinValue = reader.GetDouble("min_value"),
                        MaxValue = reader.GetDouble("max_value"),
                        AvgValue = reader.GetDouble("avg_value"),
                        Format = reader.GetString("format")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Konnte Historie nicht aus der Datenbank laden.");
                throw;
            }

            return list;
        }

    }
}
