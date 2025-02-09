<![CDATA[namespace Common.Models;

public class ValidationEvent
{
    public string EquipmentId { get; set; } = string.Empty;
    public string CardId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public ValidationResult Result { get; set; }
    public long Sequence { get; set; }
    public string SessionId { get; set; } = string.Empty;
}

public enum ValidationResult
{
    Success,
    Failure,
    Error
}]]>