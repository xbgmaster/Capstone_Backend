using JobNet.Api.Middleware;
using JobNet.Domain.Enums;
using JobNet.Infrastructure.Auth;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Controllers;

[ApiController]
[Route("api/workers")]
public class WorkersController : ControllerBase
{
    private readonly IWorkerProfileService _service;
    private readonly ICurrentUser _currentUser;

    public WorkersController(IWorkerProfileService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<WorkerProfileDto>> Get(Guid userId, CancellationToken ct)
        => (await _service.GetAsync(userId, ct)).ToActionResult();

    [Authorize(Roles = nameof(UserRole.Worker))]
    [HttpGet("me")]
    public async Task<ActionResult<WorkerProfileDto>> GetMe(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        return (await _service.GetAsync(_currentUser.UserId.Value, ct)).ToActionResult();
    }

    [Authorize(Roles = nameof(UserRole.Worker))]
    [HttpPut("me")]
    public async Task<ActionResult<WorkerProfileDto>> UpsertMe([FromBody] UpsertWorkerProfileRequest req, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        return (await _service.UpsertAsync(_currentUser.UserId.Value, req, ct)).ToActionResult();
    }
}
