using JobNet.Domain.Documents;
using JobNet.Infrastructure.Auth;
using JobNet.Infrastructure.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace JobNet.Infrastructure.Auditing;

/// <summary>
/// Writes audit entries and domain events to MongoDB. If Mongo is unreachable, logs locally
/// instead of throwing -- the business write to SQL Server is what must succeed.
/// </summary>
public class MongoAuditLogger : IAuditLogger
{
    private readonly MongoContext _mongo;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<MongoAuditLogger> _log;

    public MongoAuditLogger(MongoContext mongo, ICurrentUser currentUser, ILogger<MongoAuditLogger> log)
    {
        _mongo = mongo;
        _currentUser = currentUser;
        _log = log;
    }

    public async Task LogAsync(string action, string? entityType = null, string? entityId = null,
        IDictionary<string, object?>? metadata = null, CancellationToken ct = default)
    {
        var entry = new AuditLog
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId,
            UserEmail = _currentUser.Email,
            UserRole = _currentUser.Role?.ToString(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            Metadata = metadata is null ? null : new Dictionary<string, object?>(metadata),
        };

        try
        {
            await _mongo.AuditLogs.InsertOneAsync(entry, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "MongoDB audit write failed for action {Action}; falling back to local log.", action);
        }
    }

    public async Task PublishEventAsync(string eventType, string aggregateType, string aggregateId,
        IDictionary<string, object?>? payload = null, CancellationToken ct = default)
    {
        var evt = new DomainEvent
        {
            Id = ObjectId.GenerateNewId().ToString(),
            OccurredAt = DateTime.UtcNow,
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            ActorId = _currentUser.UserId,
            Payload = payload is null ? new() : new Dictionary<string, object?>(payload),
        };

        try
        {
            await _mongo.DomainEvents.InsertOneAsync(evt, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "MongoDB event publish failed for event {EventType}.", eventType);
        }
    }
}
