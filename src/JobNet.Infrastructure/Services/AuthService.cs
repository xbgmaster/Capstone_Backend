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

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly JobNetDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IAuditLogger _audit;

    public AuthService(JobNetDbContext db, IPasswordHasher hasher, IJwtTokenService jwt, IAuditLogger audit)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _audit = audit;
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
}
