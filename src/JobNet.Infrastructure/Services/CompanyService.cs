using JobNet.Domain.Enums;
using JobNet.Infrastructure.Auditing;
using JobNet.Infrastructure.Auth;
using JobNet.Infrastructure.Common;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Mapping;
using JobNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobNet.Infrastructure.Services;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanyDto>> ListAsync(string? query, CancellationToken ct = default);
    Task<Result<CompanyDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<CompanyDto>> UpdateAsync(Guid id, UpdateCompanyRequest req, CancellationToken ct = default);
    Task<Result<CompanyDto>> SetVerifiedAsync(Guid id, bool verified, CancellationToken ct = default);
}

public class CompanyService : ICompanyService
{
    private readonly JobNetDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public CompanyService(JobNetDbContext db, ICurrentUser currentUser, IAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IReadOnlyList<CompanyDto>> ListAsync(string? query, CancellationToken ct = default)
    {
        var q = _db.Companies.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var t = query.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(t) || (c.Industry ?? string.Empty).ToLower().Contains(t));
        }
        var rows = await q.OrderByDescending(c => c.Rating).ToListAsync(ct);
        return rows.Select(c => c.ToDto()).ToList();
    }

    public async Task<Result<CompanyDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result.Fail<CompanyDto>("Company not found.", ErrorCode.NotFound);
        return Result.Ok(c.ToDto());
    }

    public async Task<Result<CompanyDto>> UpdateAsync(Guid id, UpdateCompanyRequest req, CancellationToken ct = default)
    {
        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result.Fail<CompanyDto>("Company not found.", ErrorCode.NotFound);

        var isAdmin = _currentUser.Role == UserRole.Admin;
        var isOwner = _currentUser.UserId == c.OwnerId;
        if (!isAdmin && !isOwner)
            return Result.Fail<CompanyDto>("Only the company owner or an admin can update this company.", ErrorCode.Forbidden);

        c.Name = req.Name;
        c.Industry = req.Industry;
        c.BusinessNumber = req.BusinessNumber;
        c.Website = req.Website;
        c.Email = req.Email;
        c.Phone = req.Phone;
        c.Address = req.Address;
        c.City = req.City;
        c.Province = req.Province;
        c.FoundedYear = req.FoundedYear;
        c.EmployeeCount = req.EmployeeCount;
        c.Description = req.Description;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Company.Updated", "Company", c.Id.ToString(), ct: ct);
        return Result.Ok(c.ToDto());
    }

    public async Task<Result<CompanyDto>> SetVerifiedAsync(Guid id, bool verified, CancellationToken ct = default)
    {
        if (_currentUser.Role != UserRole.Admin)
            return Result.Fail<CompanyDto>("Only admins can change verification status.", ErrorCode.Forbidden);

        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result.Fail<CompanyDto>("Company not found.", ErrorCode.NotFound);

        c.Verified = verified;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(verified ? "Company.Verified" : "Company.Unverified",
            "Company", c.Id.ToString(), ct: ct);
        return Result.Ok(c.ToDto());
    }
}
