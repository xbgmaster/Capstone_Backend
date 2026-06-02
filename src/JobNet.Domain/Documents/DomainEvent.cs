namespace JobNet.Domain.Documents;

/// <summary>
/// Stored in MongoDB. Append-only event log of significant domain events that downstream services
/// (notifications, analytics, real-time fan-out) can consume.
/// </summary>
public class DomainEvent
{
    public string Id { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = string.Empty;     // e.g. "ApplicationSubmitted"
    public string AggregateType { get; set; } = string.Empty; // e.g. "Job"
    public string AggregateId { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public Dictionary<string, object?> Payload { get; set; } = new();
}
