using JobNet.Api.Middleware;
using JobNet.Infrastructure.Auth;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(INotificationService notifications, ICurrentUser currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> ListMine(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        return Ok(await _notifications.ListForUserAsync(_currentUser.UserId.Value, ct));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<ActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        return (await _notifications.MarkReadAsync(id, _currentUser.UserId.Value, ct)).ToActionResult();
    }

    [HttpPost("me/read-all")]
    public async Task<ActionResult> MarkAllRead(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        await _notifications.MarkAllReadAsync(_currentUser.UserId.Value, ct);
        return NoContent();
    }
}
