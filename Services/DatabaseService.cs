using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.IO;
using SystemEye.Models;

namespace SystemEye.Services
{
    public class DatabaseService
    {
        private DatabaseConfig _config;
        private bool _isInitialized = false;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(DatabaseConfig config, ILogger<DatabaseService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public void UpdateConfig(DatabaseConfig newConfig) => _config = newConfig;

        private async Task InitializeDatabaseAsync()
        {
            if (_isInitialized) return;
            try
            {
                var directory = Path.GetDirectoryName(_config.FilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var connection = new SqliteConnection(_config.GetConnectionString());
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS minute_data (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        timestamp DATETIME NOT NULL,
                        name TEXT NOT NULL,
                        hardware_type TEXT,
                        min_value REAL,
                        max_value REAL,
                        avg_value REAL,
                        format TEXT
                    );";
                await cmd.ExecuteNonQueryAsync();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Initialisieren der SQLite-Datenbank.");
            }
        }

        public async Task SaveAggregatedDataAsync(List<AggregatedSensorData> datalist, string tableName)
        {
            if (datalist.Count == 0 || string.IsNullOrEmpty(_config.FilePath)) return;
            await InitializeDatabaseAsync();

            try
            {
                using var connection = new SqliteConnection(_config.GetConnectionString());
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;

                cmd.CommandText = $"INSERT INTO {tableName} (timestamp, name, hardware_type, min_value, max_value, avg_value, format) " +
                                  "VALUES (@Timestamp, @name, @hardware_type, @min_value, @max_value, @avg_value, @format)";

                var pTime = cmd.Parameters.Add("@Timestamp", SqliteType.Text);
                var pName = cmd.Parameters.Add("@name", SqliteType.Text);
                var pHardwareType = cmd.Parameters.Add("@hardware_type", SqliteType.Text);
                var pMinValue = cmd.Parameters.Add("@min_value", SqliteType.Real);
                var pMaxValue = cmd.Parameters.Add("@max_value", SqliteType.Real);
                var pAvgValue = cmd.Parameters.Add("@avg_value", SqliteType.Real);
                var pFormat = cmd.Parameters.Add("@format", SqliteType.Text);

                foreach (var data in datalist)
                {
                    pTime.Value = data.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    pName.Value = data.Name;
                    pHardwareType.Value = data.HardwareType;
                    pMinValue.Value = data.MinValue;
                    pMaxValue.Value = data.MaxValue;
                    pAvgValue.Value = data.AvgValue;
                    pFormat.Value = data.Format;
                    await cmd.ExecuteNonQueryAsync();
                }
                transaction.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Speichern in SQLite.");
            }
        }

        public async Task<List<AggregatedSensorData>> LoadMinuteDataAsync()
        {
            var list = new List<AggregatedSensorData>();
            if (string.IsNullOrEmpty(_config.FilePath)) return list;
            await InitializeDatabaseAsync();

            try
            {
                using var connection = new SqliteConnection(_config.GetConnectionString());
                await connection.OpenAsync();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM minute_data ORDER BY timestamp DESC LIMIT 1000;";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new AggregatedSensorData
                    {
                        Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("timestamp"))),
                        Name = reader.GetString(reader.GetOrdinal("name")),
                        HardwareType = reader.GetString(reader.GetOrdinal("hardware_type")),
                        MinValue = reader.GetDouble(reader.GetOrdinal("min_value")),
                        MaxValue = reader.GetDouble(reader.GetOrdinal("max_value")),
                        AvgValue = reader.GetDouble(reader.GetOrdinal("avg_value")),
                        Format = reader.GetString(reader.GetOrdinal("format"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Historie.");
                throw;
            }
            return list;

        }
    }
}