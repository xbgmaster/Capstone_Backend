namespace JobNet.Infrastructure.Auditing;

public interface IAuditLogger
{
    Task LogAsync(string action, string? entityType = null, string? entityId = null,
        IDictionary<string, object?>? metadata = null, CancellationToken ct = default);

    Task PublishEventAsync(string eventType, string aggregateType, string aggregateId,
        IDictionary<string, object?>? payload = null, CancellationToken ct = default);
}
