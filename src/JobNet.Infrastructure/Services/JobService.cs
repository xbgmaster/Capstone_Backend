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

public interface IJobService
{
    Task<PagedResult<JobDto>> ListAsync(JobFilter filter, CancellationToken ct = default);
    Task<Result<JobDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<JobDto>> CreateAsync(CreateJobRequest req, CancellationToken ct = default);
    Task<Result<JobDto>> UpdateAsync(Guid id, UpdateJobRequest req, CancellationToken ct = default);
    Task<Result<JobDto>> ChangeStatusAsync(Guid id, JobStatus status, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public class JobService : IJobService
{
    private readonly JobNetDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public JobService(JobNetDbContext db, ICurrentUser currentUser, IAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<PagedResult<JobDto>> ListAsync(JobFilter filter, CancellationToken ct = default)
    {
        var q = _db.Jobs
            .Include(j => j.Company)
            .Include(j => j.SkillsRequired)
            .Include(j => j.Applications)
            .AsNoTracking()
            .AsQueryable();

        if (filter.OnlyOpen == true) q = q.Where(j => j.Status == JobStatus.Open);
        if (filter.CompanyId.HasValue) q = q.Where(j => j.CompanyId == filter.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Category) && filter.Category != "All")
            q = q.Where(j => j.Category == filter.Category);
        if (!string.IsNullOrWhiteSpace(filter.Province) && filter.Province != "All")
            q = q.Where(j => j.Location.EndsWith(filter.Province));
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var t = filter.Query.Trim().ToLower();
            q = q.Where(j =>
                j.Title.ToLower().Contains(t) ||
                j.Description.ToLower().Contains(t) ||
                j.SkillsRequired.Any(s => s.Name.ToLower().Contains(t)));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var rows = await q
            .OrderByDescending(j => j.PostedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<JobDto>(rows.Select(j => j.ToDto()).ToList(), total, page, pageSize);
    }

    public async Task<Result<JobDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var j = await _db.Jobs
            .Include(x => x.Company)
            .Include(x => x.SkillsRequired)
            .Include(x => x.Applications)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (j is null) return Result.Fail<JobDto>("Job not found.", ErrorCode.NotFound);
        return Result.Ok(j.ToDto());
    }

    public async Task<Result<JobDto>> CreateAsync(CreateJobRequest req, CancellationToken ct = default)
    {
        if (_currentUser.Role != UserRole.Employer || !_currentUser.CompanyId.HasValue)
            return Result.Fail<JobDto>("Only employers with a company can post jobs.", ErrorCode.Forbidden);

        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Description) || string.IsNullOrWhiteSpace(req.Location))
            return Result.Fail<JobDto>("Title, description, and location are required.", ErrorCode.Validation);

        var job = new Job
        {
            CompanyId = _currentUser.CompanyId.Value,
            Title = req.Title,
            Category = string.IsNullOrWhiteSpace(req.Category) ? "Other" : req.Category,
            Description = req.Description,
            Activity = req.Activity,
            Location = req.Location,
            DueDate = req.DueDate,
            PaymentType = req.PaymentType,
            PaymentAmount = req.PaymentAmount,
            Status = JobStatus.Open,
            SkillsRequired = (req.SkillsRequired ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(s => new JobSkill { Name = s.Trim() })
                .ToList(),
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Job.Created", "Job", job.Id.ToString(),
            metadata: new Dictionary<string, object?> { ["title"] = job.Title }, ct: ct);
        await _audit.PublishEventAsync("JobPosted", "Job", job.Id.ToString(),
            new Dictionary<string, object?> { ["companyId"] = job.CompanyId, ["title"] = job.Title }, ct);

        // Reload with includes for the DTO
        var saved = await _db.Jobs
            .Include(x => x.Company).Include(x => x.SkillsRequired).Include(x => x.Applications)
            .AsNoTracking().FirstAsync(x => x.Id == job.Id, ct);
        return Result.Ok(saved.ToDto());
    }

    public async Task<Result<JobDto>> UpdateAsync(Guid id, UpdateJobRequest req, CancellationToken ct = default)
    {
        var job = await _db.Jobs.Include(x => x.SkillsRequired).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return Result.Fail<JobDto>("Job not found.", ErrorCode.NotFound);

        if (!CanManage(job)) return Result.Fail<JobDto>("You can't manage this job.", ErrorCode.Forbidden);

        job.Title = req.Title;
        job.Category = req.Category;
        job.Description = req.Description;
        job.Activity = req.Activity;
        job.Location = req.Location;
        job.DueDate = req.DueDate;
        job.PaymentType = req.PaymentType;
        job.PaymentAmount = req.PaymentAmount;

        // Replace the skill rows. We must explicitly Add the new ones: assigning
        // them only through the navigation makes EF treat their client-generated
        // Ids (JobSkill.Id has a Guid.NewGuid() initializer + ValueGeneratedOnAdd)
        // as existing rows and emit UPDATEs that affect 0 rows -> DbUpdateConcurrencyException.
        _db.JobSkills.RemoveRange(job.SkillsRequired);
        var newSkills = (req.SkillsRequired ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(s => new JobSkill { JobId = job.Id, Name = s.Trim() })
            .ToList();
        _db.JobSkills.AddRange(newSkills);
        job.SkillsRequired = newSkills;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Job.Updated", "Job", job.Id.ToString(), ct: ct);

        var saved = await _db.Jobs
            .Include(x => x.Company).Include(x => x.SkillsRequired).Include(x => x.Applications)
            .AsNoTracking().FirstAsync(x => x.Id == job.Id, ct);
        return Result.Ok(saved.ToDto());
    }

    public async Task<Result<JobDto>> ChangeStatusAsync(Guid id, JobStatus status, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return Result.Fail<JobDto>("Job not found.", ErrorCode.NotFound);
        if (!CanManage(job)) return Result.Fail<JobDto>("You can't manage this job.", ErrorCode.Forbidden);

        job.Status = status;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync($"Job.Status.{status}", "Job", job.Id.ToString(), ct: ct);

        var saved = await _db.Jobs
            .Include(x => x.Company).Include(x => x.SkillsRequired).Include(x => x.Applications)
            .AsNoTracking().FirstAsync(x => x.Id == job.Id, ct);
        return Result.Ok(saved.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return Result.Fail("Job not found.", ErrorCode.NotFound);
        if (!CanManage(job)) return Result.Fail("You can't delete this job.", ErrorCode.Forbidden);

        _db.Jobs.Remove(job);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Job.Deleted", "Job", job.Id.ToString(), ct: ct);
        return Result.Ok();
    }

    private bool CanManage(Job job)
    {
        if (_currentUser.Role == UserRole.Admin) return true;
        return _currentUser.Role == UserRole.Employer && _currentUser.CompanyId == job.CompanyId;
    }
}
