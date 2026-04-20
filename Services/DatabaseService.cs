using Microsoft.Extensions.Logging;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
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
                cmd.CommandText = $"INSERT INTO {tableName}(timestamp, name, hardware_type, min_value, max_value, avg_value, format)";

                var pTime = cmd.Parameters.Add("@Timestamp", MySqlDbType.DateTime);
                var pName = cmd.Parameters.Add("@name", MySqlDbType.VarChar);
                var pHardwareType = cmd.Parameters.Add("@hardware_type", MySqlDbType.VarChar);
                var pMinValue = cmd.Parameters.Add("@min_value", MySqlDbType.Double);
                var pMaxValue = cmd.Parameters.Add("@max_value", MySqlDbType.Double);
                var pAvgValue = cmd.Parameters.Add("@avg_value", MySqlDbType.Double);
                var pFormat = cmd.Parameters.Add("@format", MySqlDbType.VarChar);

                foreach(var data in datalist)
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
    }
}
