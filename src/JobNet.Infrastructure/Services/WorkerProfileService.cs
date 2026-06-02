using JobNet.Domain.Entities;
using JobNet.Infrastructure.Auditing;
using JobNet.Infrastructure.Common;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Mapping;
using JobNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobNet.Infrastructure.Services;

public interface IWorkerProfileService
{
    Task<Result<WorkerProfileDto>> GetAsync(Guid userId, CancellationToken ct = default);
    Task<Result<WorkerProfileDto>> UpsertAsync(Guid userId, UpsertWorkerProfileRequest req, CancellationToken ct = default);
}

public class WorkerProfileService : IWorkerProfileService
{
    private readonly JobNetDbContext _db;
    private readonly IAuditLogger _audit;

    public WorkerProfileService(JobNetDbContext db, IAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Result<WorkerProfileDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var p = await _db.WorkerProfiles
            .Include(x => x.Skills)
            .Include(x => x.Certifications)
            .Include(x => x.Experiences)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (p is null) return Result.Fail<WorkerProfileDto>("Worker profile not found.", ErrorCode.NotFound);
        return Result.Ok(p.ToDto());
    }

    public async Task<Result<WorkerProfileDto>> UpsertAsync(Guid userId, UpsertWorkerProfileRequest req, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Fail<WorkerProfileDto>("User not found.", ErrorCode.NotFound);

        if (!string.IsNullOrWhiteSpace(req.FirstName)) user.FirstName = req.FirstName;
        if (!string.IsNullOrWhiteSpace(req.LastName)) user.LastName = req.LastName;
        if (req.Phone is not null) user.Phone = req.Phone;
        if (req.City is not null) user.City = req.City;
        if (req.Province is not null) user.Province = req.Province;

        var p = await _db.WorkerProfiles
            .Include(x => x.Skills)
            .Include(x => x.Certifications)
            .Include(x => x.Experiences)
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (p is null)
        {
            p = new WorkerProfile { UserId = userId };
            _db.WorkerProfiles.Add(p);
        }

        p.Headline = req.Headline;
        p.Bio = req.Bio;
        p.YearsExperience = req.YearsExperience;
        p.HourlyRate = req.HourlyRate;
        p.Availability = string.IsNullOrWhiteSpace(req.Availability) ? "Flexible" : req.Availability;

        // Replace child collections wholesale - simplest and predictable for the MVP.
        _db.WorkerSkills.RemoveRange(p.Skills);
        p.Skills = (req.Skills ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new WorkerSkill { WorkerId = userId, Name = name.Trim() })
            .ToList();

        _db.Certifications.RemoveRange(p.Certifications);
        p.Certifications = (req.Certifications ?? Array.Empty<CertificationDto>())
            .Select(c => new Certification { WorkerId = userId, Name = c.Name, Issuer = c.Issuer, Year = c.Year })
            .ToList();

        _db.Experiences.RemoveRange(p.Experiences);
        p.Experiences = (req.Experiences ?? Array.Empty<ExperienceDto>())
            .Select(e => new Experience { WorkerId = userId, Title = e.Title, Company = e.Company, From = e.From, To = e.To })
            .ToList();

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("WorkerProfile.Updated", "WorkerProfile", userId.ToString(), ct: ct);

        return Result.Ok(p.ToDto());
    }
}
