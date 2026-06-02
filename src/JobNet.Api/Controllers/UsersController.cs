using JobNet.Api.Middleware;
using JobNet.Domain.Enums;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(
        [FromQuery] UserRole? role,
        [FromQuery] string? query,
        CancellationToken ct)
        => Ok(await _users.ListAsync(role, query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> Get(Guid id, CancellationToken ct)
        => (await _users.GetAsync(id, ct)).ToActionResult();

    public record ChangeStatusRequest(UserStatus Status);

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<UserDto>> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest req, CancellationToken ct)
        => (await _users.SetStatusAsync(id, req.Status, ct)).ToActionResult();
}
