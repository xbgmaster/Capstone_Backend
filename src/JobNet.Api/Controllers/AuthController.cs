using JobNet.Api.Middleware;
using JobNet.Infrastructure.Auth;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _currentUser;

    public AuthController(IAuthService auth, ICurrentUser currentUser)
    {
        _auth = auth;
        _currentUser = currentUser;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req, CancellationToken ct)
        => (await _auth.RegisterAsync(req, ct)).ToActionResult();

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
        => (await _auth.LoginAsync(req, ct)).ToActionResult();

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        return (await _auth.GetMeAsync(_currentUser.UserId.Value, ct)).ToActionResult();
    }

    /// <summary>
    /// Stub for forgot-password to match the frontend flow. A real implementation would
    /// generate a one-time token, persist it, and send an email via SendGrid/SES.
    /// </summary>
    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword([FromBody] ForgotPasswordRequest req)
        => Ok(new { ok = true, message = "If an account exists, a reset link has been emailed." });

    public record ForgotPasswordRequest(string Email);
}
