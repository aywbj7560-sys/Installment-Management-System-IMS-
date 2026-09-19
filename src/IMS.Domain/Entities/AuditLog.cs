namespace IMS.Domain.Entities;

public class AuditLog
{
    public long AuditLogId { get; set; }
    public long UserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string TargetEntityType { get; set; } = string.Empty;
    public long TargetEntityId { get; set; }
    public DateTime Timestamp { get; set; }
    public string PreviousState { get; set; } = string.Empty;
    public string NewState { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Note { get; set; }

    public User User { get; set; } = null!;
}
