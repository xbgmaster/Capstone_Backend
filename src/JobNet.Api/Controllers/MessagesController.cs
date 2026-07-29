using JobNet.Api.Middleware;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messages;

    public MessagesController(IMessageService messages)
    {
        _messages = messages;
    }

    /// <summary>Send a message to another user (optionally scoped to a job).</summary>
    [HttpPost]
    public async Task<ActionResult<MessageDto>> Send([FromBody] SendMessageRequest req, CancellationToken ct)
        => (await _messages.SendAsync(req, ct)).ToActionResult();

    /// <summary>
    /// Get the full conversation with another user. Pass an optional jobId to
    /// scope the thread to a specific application. Marks incoming messages read.
    /// </summary>
    [HttpGet("conversation")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> Conversation(
        [FromQuery] Guid withUserId, [FromQuery] Guid? jobId, CancellationToken ct)
        => (await _messages.GetConversationAsync(withUserId, jobId, ct)).ToActionResult();

    /// <summary>List conversation summaries for the signed-in user (inbox).</summary>
    [HttpGet("threads")]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryDto>>> Threads(CancellationToken ct)
        => Ok(await _messages.ListThreadsAsync(ct));

    /// <summary>Number of unread messages for the signed-in user.</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> UnreadCount(CancellationToken ct)
        => Ok(new { count = await _messages.UnreadCountAsync(ct) });
}
