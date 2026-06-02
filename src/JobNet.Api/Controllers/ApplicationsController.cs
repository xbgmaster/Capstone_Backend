using JobNet.Api.Middleware;
using JobNet.Domain.Enums;
using JobNet.Infrastructure.Auth;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _apps;
    private readonly ICurrentUser _currentUser;

    public ApplicationsController(IApplicationService apps, ICurrentUser currentUser)
    {
        _apps = apps;
        _currentUser = currentUser;
    }

    [Authorize(Roles = nameof(UserRole.Worker))]
    [HttpPost]
    public async Task<ActionResult<ApplicationDto>> Apply([FromBody] CreateApplicationRequest req, CancellationToken ct)
        => (await _apps.ApplyAsync(req, ct)).ToActionResult();

    [Authorize(Roles = nameof(UserRole.Worker))]
    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> ListMine(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        return Ok(await _apps.ListMineAsync(_currentUser.UserId.Value, ct));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ApplicationDto>> ChangeStatus(Guid id, [FromBody] ChangeApplicationStatusRequest req, CancellationToken ct)
        => (await _apps.ChangeStatusAsync(id, req.Status, ct)).ToActionResult();
}
