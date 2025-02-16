namespace Common.Models;

public class ValidationEvent
{
    public string EquipmentId { get; set; } = string.Empty;
    public string TokenId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();

    // Backwards compatibility for older code using CardId and Result
    public string CardId 
    { 
        get => TokenId;
        set => TokenId = value;
    }

    public ValidationResult Result
    {
        get => Status.ToUpper() switch
        {
            "SUCCESS" => ValidationResult.Success,
            "FAILURE" => ValidationResult.Failure,
            _ => ValidationResult.Error
        };
        set => Status = value.ToString().ToUpper();
    }
}

public enum ValidationResult
{
    Success,
    Failure,
    Error
}