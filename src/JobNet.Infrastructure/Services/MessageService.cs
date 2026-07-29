using JobNet.Domain.Entities;
using JobNet.Domain.Enums;
using JobNet.Infrastructure.Auditing;
using JobNet.Infrastructure.Auth;
using JobNet.Infrastructure.Common;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Mapping;
using JobNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobNet.Infrastructure.Services;

public interface IMessageService
{
    Task<Result<MessageDto>> SendAsync(SendMessageRequest req, CancellationToken ct = default);
    Task<Result<IReadOnlyList<MessageDto>>> GetConversationAsync(Guid otherUserId, Guid? jobId, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationSummaryDto>> ListThreadsAsync(CancellationToken ct = default);
    Task<int> UnreadCountAsync(CancellationToken ct = default);
}

public class MessageService : IMessageService
{
    private readonly JobNetDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly INotificationService _notify;

    public MessageService(JobNetDbContext db, ICurrentUser currentUser, IAuditLogger audit, INotificationService notify)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notify = notify;
    }

    public async Task<Result<MessageDto>> SendAsync(SendMessageRequest req, CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Fail<MessageDto>("You must be signed in to send messages.", ErrorCode.Unauthorized);

        var senderId = _currentUser.UserId.Value;

        if (string.IsNullOrWhiteSpace(req.Body))
            return Result.Fail<MessageDto>("Message body is required.", ErrorCode.Validation);
        if (req.RecipientId == Guid.Empty || req.RecipientId == senderId)
            return Result.Fail<MessageDto>("Invalid recipient.", ErrorCode.Validation);

        var recipient = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.RecipientId, ct);
        if (recipient is null)
            return Result.Fail<MessageDto>("Recipient not found.", ErrorCode.NotFound);

        if (req.JobId.HasValue && !await _db.Jobs.AnyAsync(j => j.Id == req.JobId.Value, ct))
            return Result.Fail<MessageDto>("Job not found.", ErrorCode.NotFound);

        var sender = await _db.Users.FirstAsync(u => u.Id == senderId, ct);

        var message = new Message
        {
            SenderId = senderId,
            RecipientId = req.RecipientId,
            JobId = req.JobId,
            Body = req.Body.Trim(),
            Read = false,
        };
        _db.Messages.Add(message);

        _notify.Enqueue(
            recipient.Id,
            NotificationType.Message,
            "New message",
            $"{sender.FirstName} {sender.LastName} sent you a message.",
            "/messages");

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Message.Sent", "Message", message.Id.ToString(),
            metadata: new Dictionary<string, object?> { ["recipientId"] = recipient.Id, ["jobId"] = req.JobId }, ct: ct);

        return Result.Ok(await BuildDtoAsync(message.Id, ct));
    }

    public async Task<Result<IReadOnlyList<MessageDto>>> GetConversationAsync(Guid otherUserId, Guid? jobId, CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Fail<IReadOnlyList<MessageDto>>("You must be signed in.", ErrorCode.Unauthorized);

        var me = _currentUser.UserId.Value;

        var q = _db.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Include(m => m.Job)
            .Where(m =>
                (m.SenderId == me && m.RecipientId == otherUserId) ||
                (m.SenderId == otherUserId && m.RecipientId == me));

        if (jobId.HasValue) q = q.Where(m => m.JobId == jobId.Value);

        var rows = await q.OrderBy(m => m.CreatedAt).ToListAsync(ct);

        // Mark the incoming messages as read now that the user is viewing them.
        var unread = rows.Where(m => m.RecipientId == me && !m.Read).ToList();
        if (unread.Count > 0)
        {
            foreach (var m in unread) m.Read = true;
            await _db.SaveChangesAsync(ct);
        }

        return Result.Ok<IReadOnlyList<MessageDto>>(rows.Select(m => m.ToDto()).ToList());
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> ListThreadsAsync(CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) return Array.Empty<ConversationSummaryDto>();
        var me = _currentUser.UserId.Value;

        var rows = await _db.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Include(m => m.Job)
            .Where(m => m.SenderId == me || m.RecipientId == me)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        // Group by (other user + job) so each application thread is distinct.
        return rows
            .GroupBy(m => new { OtherId = m.SenderId == me ? m.RecipientId : m.SenderId, m.JobId })
            .Select(g =>
            {
                var latest = g.First();
                var other = latest.SenderId == me ? latest.Recipient : latest.Sender;
                return new ConversationSummaryDto(
                    g.Key.OtherId,
                    other is null ? string.Empty : $"{other.FirstName} {other.LastName}".Trim(),
                    g.Key.JobId,
                    latest.Job?.Title,
                    latest.Body,
                    latest.CreatedAt,
                    g.Count(m => m.RecipientId == me && !m.Read));
            })
            .OrderByDescending(t => t.LastMessageAt)
            .ToList();
    }

    public async Task<int> UnreadCountAsync(CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue) return 0;
        var me = _currentUser.UserId.Value;
        return await _db.Messages.CountAsync(m => m.RecipientId == me && !m.Read, ct);
    }

    private async Task<MessageDto> BuildDtoAsync(Guid id, CancellationToken ct)
    {
        var m = await _db.Messages
            .Include(x => x.Sender)
            .Include(x => x.Recipient)
            .Include(x => x.Job)
            .AsNoTracking()
            .FirstAsync(x => x.Id == id, ct);
        return m.ToDto();
    }
}
