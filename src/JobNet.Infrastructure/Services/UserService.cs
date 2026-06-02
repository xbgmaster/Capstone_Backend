using JobNet.Domain.Enums;
using JobNet.Infrastructure.Auditing;
using JobNet.Infrastructure.Common;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Mapping;
using JobNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobNet.Infrastructure.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(UserRole? role, string? query, CancellationToken ct = default);
    Task<Result<UserDto>> SetStatusAsync(Guid userId, UserStatus status, CancellationToken ct = default);
    Task<Result<UserDto>> GetAsync(Guid userId, CancellationToken ct = default);
}

public class UserService : IUserService
{
    private readonly JobNetDbContext _db;
    private readonly IAuditLogger _audit;

    public UserService(JobNetDbContext db, IAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(UserRole? role, string? query, CancellationToken ct = default)
    {
        IQueryable<Domain.Entities.User> q = _db.Users;
        if (role.HasValue) q = q.Where(u => u.Role == role.Value);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var t = query.Trim().ToLower();
            q = q.Where(u =>
                u.FirstName.ToLower().Contains(t) ||
                u.LastName.ToLower().Contains(t) ||
                u.Email.ToLower().Contains(t));
        }
        var rows = await q.OrderByDescending(u => u.CreatedAt).ToListAsync(ct);
        return rows.Select(u => u.ToDto()).ToList();
    }

    public async Task<Result<UserDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null) return Result.Fail<UserDto>("User not found.", ErrorCode.NotFound);
        return Result.Ok(u.ToDto());
    }

    public async Task<Result<UserDto>> SetStatusAsync(Guid userId, UserStatus status, CancellationToken ct = default)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null) return Result.Fail<UserDto>("User not found.", ErrorCode.NotFound);

        u.Status = status;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            status == UserStatus.Suspended ? "User.Suspended" : "User.Reactivated",
            "User", u.Id.ToString(), ct: ct);

        return Result.Ok(u.ToDto());
    }
}
