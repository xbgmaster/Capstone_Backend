namespace JobNet.Domain.Documents;

/// <summary>
/// Stored in MongoDB. One document per significant business action (login, register, post job, apply, etc).
/// </summary>
public class AuditLog
{
    public string Id { get; set; } = string.Empty;       // ObjectId hex string
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }
    public string Action { get; set; } = string.Empty;   // e.g. "Job.Created", "Application.Selected"
    public string? EntityType { get; set; }              // e.g. "Job", "Application"
    public string? EntityId { get; set; }                // entity Guid as string
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Dictionary<string, object?>? Metadata { get; set; }
}
