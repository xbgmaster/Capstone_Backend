using JobNet.Domain.Entities;
using JobNet.Domain.Enums;
using JobNet.Infrastructure.Common;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Mapping;
using JobNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobNet.Infrastructure.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result> MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Add a notification (used by other services); does NOT call SaveChanges.</summary>
    void Enqueue(Guid userId, NotificationType type, string title, string message, string? link = null);
}

public class NotificationService : INotificationService
{
    private readonly JobNetDbContext _db;

    public NotificationService(JobNetDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NotificationDto>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
        return rows.Select(n => n.ToDto()).ToList();
    }

    public async Task<Result> MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);
        if (n is null) return Result.Fail("Notification not found.", ErrorCode.NotFound);
        n.Read = true;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.Read).ToListAsync(ct);
        foreach (var n in unread) n.Read = true;
        await _db.SaveChangesAsync(ct);
    }

    public void Enqueue(Guid userId, NotificationType type, string title, string message, string? link = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Link = link,
            Read = false,
        });
    }
}
