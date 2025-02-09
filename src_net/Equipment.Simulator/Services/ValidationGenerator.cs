using Common.Models;

namespace Equipment.Simulator.Services;

public class ValidationGenerator
{
    private readonly string _equipmentId;
    private readonly string _sessionId;
    private static readonly string[] Locations = {
        "STATION-NORD", "STATION-SUD", "STATION-EST", 
        "STATION-OUEST", "STATION-CENTRE"
    };

    public ValidationGenerator(string equipmentId)
    {
        _equipmentId = equipmentId;
        _sessionId = $"SESSION-{Guid.NewGuid():N}";
    }

    public ValidationEvent GenerateValidation(long sequence)
    {
        return new ValidationEvent
        {
            EquipmentId = _equipmentId,
            CardId = $"CARD-{Random.Shared.Next(1, 1000000):D6}",
            Timestamp = DateTime.UtcNow,
            Location = Locations[Random.Shared.Next(Locations.Length)],
            Amount = Random.Shared.Next(100, 500) / 100.0m,
            Result = (ValidationResult)Random.Shared.Next(3),
            Sequence = sequence,
            SessionId = _sessionId
        };
    }
}