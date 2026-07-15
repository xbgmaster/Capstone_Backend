using JobNet.Domain.Entities;
using JobNet.Domain.Enums;
using JobNet.Infrastructure.Auditing;
using JobNet.Infrastructure.Auth;
using JobNet.Infrastructure.Common;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Email;
using JobNet.Infrastructure.Mapping;
using JobNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace JobNet.Infrastructure.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly JobNetDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IAuditLogger _audit;
    private readonly IEmailSender _email;
    private readonly BrevoSettings _brevo;
    private readonly ILogger<AuthService> _logger;

    public AuthService(JobNetDbContext db, IPasswordHasher hasher, IJwtTokenService jwt, IAuditLogger audit, IEmailSender email, IOptions<BrevoSettings> brevo, ILogger<AuthService> logger)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _audit = audit;
        _email = email;
        _brevo = brevo.Value;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Result.Fail<AuthResponse>("Email and password are required.", ErrorCode.Validation);
        if (req.Password.Length < 6)
            return Result.Fail<AuthResponse>("Password must be at least 6 characters.", ErrorCode.Validation);
        if (req.Role != UserRole.Worker && req.Role != UserRole.Employer)
            return Result.Fail<AuthResponse>("You can only register as a Worker or Employer.", ErrorCode.Validation);
        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == email, ct))
            return Result.Fail<AuthResponse>("An account with that email already exists.", ErrorCode.Conflict);
        var initials = (
            (string.IsNullOrEmpty(req.FirstName) ? "?" : req.FirstName[..1]) +
            (string.IsNullOrEmpty(req.LastName) ? "?" : req.LastName[..1])
        ).ToUpperInvariant();
        var user = new User
        {
            Role = req.Role,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Email = req.Email.Trim(),
            PasswordHash = _hasher.Hash(req.Password),
            Phone = req.Phone,
            City = req.City,
            Province = req.Province,
            Avatar = initials,
            Status = UserStatus.Active,
        };
        if (req.Role == UserRole.Employer)
        {
            if (string.IsNullOrWhiteSpace(req.CompanyName))
                return Result.Fail<AuthResponse>("Company name is required for employer accounts.", ErrorCode.Validation);

            // BUG-001: Circular Dependency FKs
            // User and Company reference each other (User.CompanyId -> Company.Id
            // and Company.OwnerId -> User.Id), so EF can't order them in a single
            // insert. Persist in stages inside a transaction: user, then company,
            // then link the user back to the company.
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);          // 1) user (CompanyId still null)
            var company = new Company
            {
                OwnerId = user.Id,
                Name = req.CompanyName,
                Industry = req.Industry ?? "General Services",
                Email = req.Email.Trim(),
                Phone = req.Phone,
                City = req.City,
                Province = req.Province,
                FoundedYear = DateTime.UtcNow.Year,
                EmployeeCount = "1-10",
            };
            _db.Companies.Add(company);
            await _db.SaveChangesAsync(ct);          // 2) company (OwnerId to user)
            user.CompanyId = company.Id;
            await _db.SaveChangesAsync(ct);          // 3) link user to company
            await tx.CommitAsync(ct);
        }
        else
        {
            _db.Users.Add(user);
            _db.WorkerProfiles.Add(new WorkerProfile
            {
                UserId = user.Id,
                Headline = req.Headline ?? "New on Jobnet",
                Availability = "Flexible",
            });
            await _db.SaveChangesAsync(ct);
        }
        await _audit.LogAsync("User.Registered", "User", user.Id.ToString(),
            metadata: new Dictionary<string, object?> { ["role"] = user.Role.ToString() }, ct: ct);
        var (token, expiresAt) = _jwt.IssueToken(user);
        return Result.Ok(new AuthResponse(token, expiresAt, user.ToDto()));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Result.Fail<AuthResponse>("Email and password are required.", ErrorCode.Validation);

        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        if (user is null || !_hasher.Verify(req.Password, user.PasswordHash))
        {
            await _audit.LogAsync("Auth.LoginFailed", metadata: new Dictionary<string, object?> { ["email"] = req.Email }, ct: ct);
            return Result.Fail<AuthResponse>("Invalid email or password.", ErrorCode.Unauthorized);
        }
        if (user.Status == UserStatus.Suspended)
        {
            await _audit.LogAsync("Auth.LoginBlocked", "User", user.Id.ToString(), ct: ct);
            return Result.Fail<AuthResponse>("Your account is suspended. Please contact support.", ErrorCode.Forbidden);
        }

        await _audit.LogAsync("Auth.Login", "User", user.Id.ToString(), ct: ct);

        var (token, expiresAt) = _jwt.IssueToken(user);
        return Result.Ok(new AuthResponse(token, expiresAt, user.ToDto()));
    }

    public async Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Fail<UserDto>("User not found.", ErrorCode.NotFound);
        return Result.Ok(user.ToDto());
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, ct);

        // Always behave the same way regardless of whether the account exists, so we
        // don't leak which emails are registered. Only send a real email when it does.
        if (user is not null && user.Status != UserStatus.Suspended)
        {
            var rawToken = GenerateToken();
            user.PasswordResetTokenHash = HashToken(rawToken);
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.Add(ResetTokenLifetime);
            await _db.SaveChangesAsync(ct);

            var link = BuildResetLink(user.Email, rawToken);
            var html = BuildResetEmailHtml(user.FirstName, link);

            try
            {
                await _email.SendAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(),
                    "Reset your Jobnet password", html, ct);
                await _audit.LogAsync("Auth.PasswordResetRequested", "User", user.Id.ToString(), ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}.", user.Email);
            }
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
    {
        var normalized = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, ct);

        // Generic error so we don't reveal whether the email exists or which part failed.
        if (user is null
            || string.IsNullOrEmpty(user.PasswordResetTokenHash)
            || user.PasswordResetTokenExpiresAt is null
            || user.PasswordResetTokenExpiresAt < DateTime.UtcNow
            || !TokensMatch(user.PasswordResetTokenHash, dto.Token))
        {
            throw new ValidationException("Invalid or expired reset link.");
        }

        user.PasswordHash = _hasher.Hash(dto.NewPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Auth.PasswordReset", "User", user.Id.ToString(), ct: ct);
        _logger.LogInformation("Password successfully reset for {Email}.", user.Email);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(hash);
    }

    private static bool TokensMatch(string storedHash, string providedToken)
    {
        if (string.IsNullOrEmpty(providedToken)) return false;
        var providedHash = HashToken(providedToken);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedHash), Encoding.UTF8.GetBytes(providedHash));
    }

    private string BuildResetLink(string email, string rawToken)
    {
        var baseUrl = _brevo.AppBaseUrl.TrimEnd('/');
        return $"{baseUrl}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(rawToken)}";
    }

    private static string BuildResetEmailHtml(string firstName, string link) => $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;font-size:15px;color:#1f2937"">
  <p>Hi {System.Net.WebUtility.HtmlEncode(firstName)},</p>
  <p>We received a request to reset your Jobnet password. Click the button below to choose a new one. This link expires in 1 hour.</p>
  <p style=""margin:24px 0"">
    <a href=""{link}"" style=""background:#2563eb;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none"">Reset password</a>
  </p>
  <p>If the button doesn't work, copy and paste this link into your browser:</p>
  <p><a href=""{link}"">{link}</a></p>
  <p>If you didn't request this, you can safely ignore this email.</p>
  <p>— The Jobnet team</p>
</div>";
}
