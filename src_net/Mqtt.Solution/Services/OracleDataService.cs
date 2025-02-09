<![CDATA[using System.Data;
using Oracle.ManagedDataAccess.Client;
using Common.Models;

namespace Mqtt.Solution.Services;

public class OracleDataService : IDisposable
{
    private readonly OracleConnection _connection;
    private readonly ILogger<OracleDataService> _logger;
    private const int BatchSize = 1000;

    public OracleDataService(ILogger<OracleDataService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connection = new OracleConnection(configuration.GetConnectionString("SDD_ACT"));
        _connection.Open();
    }

    public async Task BulkInsertValidationsAsync(IEnumerable<ValidationEvent> validations)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            BEGIN
                validation_pkg.bulk_insert_validations(:validations);
            END;";
        cmd.CommandType = CommandType.Text;
        cmd.ArrayBindCount = validations.Count();

        // Préparation des tableaux pour le bulk insert
        var equipmentIds = validations.Select(v => v.EquipmentId).ToArray();
        var cardIds = validations.Select(v => v.CardId).ToArray();
        var timestamps = validations.Select(v => v.Timestamp).ToArray();
        var locations = validations.Select(v => v.Location).ToArray();
        var amounts = validations.Select(v => v.Amount).ToArray();
        var results = validations.Select(v => v.Result.ToString()).ToArray();

        var validationTable = new OracleParameter
        {
            ParameterName = "validations",
            OracleDbType = OracleDbType.Array,
            Value = validations.Select(v => new object[]
            {
                v.EquipmentId,
                v.CardId,
                v.Timestamp,
                v.Location,
                v.Amount,
                v.Result.ToString()
            }).ToArray()
        };

        cmd.Parameters.Add(validationTable);

        try
        {
            await cmd.ExecuteNonQueryAsync();
            MetricsRegistry.ValidationsPersisted.Inc(validations.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du bulk insert dans Oracle");
            MetricsRegistry.PersistenceErrors.Inc();
            throw;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}]]>