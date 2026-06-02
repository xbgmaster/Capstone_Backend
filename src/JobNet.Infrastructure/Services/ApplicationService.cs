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

public interface IApplicationService
{
    Task<Result<ApplicationDto>> ApplyAsync(CreateApplicationRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationDto>> ListMineAsync(Guid workerId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ApplicationDto>>> ListForJobAsync(Guid jobId, CancellationToken ct = default);
    Task<Result<ApplicationDto>> ChangeStatusAsync(Guid id, ApplicationStatus status, CancellationToken ct = default);
}

public class ApplicationService : IApplicationService
{
    private readonly JobNetDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly INotificationService _notify;

    public ApplicationService(JobNetDbContext db, ICurrentUser currentUser, IAuditLogger audit, INotificationService notify)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notify = notify;
    }

    public async Task<Result<ApplicationDto>> ApplyAsync(CreateApplicationRequest req, CancellationToken ct = default)
    {
        if (_currentUser.Role != UserRole.Worker || !_currentUser.UserId.HasValue)
            return Result.Fail<ApplicationDto>("Only workers can apply for jobs.", ErrorCode.Forbidden);
        if (string.IsNullOrWhiteSpace(req.CoverLetter))
            return Result.Fail<ApplicationDto>("Cover letter is required.", ErrorCode.Validation);

        var job = await _db.Jobs.Include(j => j.Company).FirstOrDefaultAsync(j => j.Id == req.JobId, ct);
        if (job is null) return Result.Fail<ApplicationDto>("Job not found.", ErrorCode.NotFound);
        if (job.Status != JobStatus.Open)
            return Result.Fail<ApplicationDto>("This job is not accepting applications.", ErrorCode.Validation);

        var workerId = _currentUser.UserId.Value;
        if (await _db.Applications.AnyAsync(a => a.JobId == job.Id && a.WorkerId == workerId, ct))
            return Result.Fail<ApplicationDto>("You have already applied to this job.", ErrorCode.Conflict);

        var worker = await _db.Users.FirstOrDefaultAsync(u => u.Id == workerId, ct);
        if (worker is null) return Result.Fail<ApplicationDto>("Worker not found.", ErrorCode.NotFound);

        var app = new Application
        {
            JobId = job.Id,
            WorkerId = workerId,
            CoverLetter = req.CoverLetter.Trim(),
            ExpectedRate = req.ExpectedRate <= 0 ? job.PaymentAmount : req.ExpectedRate,
            Status = ApplicationStatus.Submitted,
        };
        _db.Applications.Add(app);

        if (job.Company is not null)
        {
            _notify.Enqueue(
                job.Company.OwnerId,
                NotificationType.Application,
                "New application",
                $"{worker.FirstName} {worker.LastName} applied to \"{job.Title}\".",
                $"/employer/jobs/{job.Id}");
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Application.Created", "Application", app.Id.ToString(),
            metadata: new Dictionary<string, object?> { ["jobId"] = job.Id }, ct: ct);
        await _audit.PublishEventAsync("ApplicationSubmitted", "Application", app.Id.ToString(),
            new Dictionary<string, object?> { ["jobId"] = job.Id, ["workerId"] = workerId }, ct);

        return Result.Ok(await BuildDtoAsync(app.Id, ct));
    }

    public async Task<IReadOnlyList<ApplicationDto>> ListMineAsync(Guid workerId, CancellationToken ct = default)
    {
        var rows = await _db.Applications
            .Where(a => a.WorkerId == workerId)
            .Include(a => a.Job)!.ThenInclude(j => j!.Company)
            .Include(a => a.Worker)!.ThenInclude(w => w!.WorkerProfile)
            .OrderByDescending(a => a.SubmittedAt)
            .AsNoTracking()
            .ToListAsync(ct);
        return rows.Select(a => a.ToDto()).ToList();
    }

    public async Task<Result<IReadOnlyList<ApplicationDto>>> ListForJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null) return Result.Fail<IReadOnlyList<ApplicationDto>>("Job not found.", ErrorCode.NotFound);

        var isAdmin = _currentUser.Role == UserRole.Admin;
        var isOwner = _currentUser.Role == UserRole.Employer && _currentUser.CompanyId == job.CompanyId;
        if (!isAdmin && !isOwner)
            return Result.Fail<IReadOnlyList<ApplicationDto>>("You can't view applications for this job.", ErrorCode.Forbidden);

        var rows = await _db.Applications
            .Where(a => a.JobId == jobId)
            .Include(a => a.Job)!.ThenInclude(j => j!.Company)
            .Include(a => a.Worker)!.ThenInclude(w => w!.WorkerProfile)
            .OrderByDescending(a => a.SubmittedAt)
            .AsNoTracking()
            .ToListAsync(ct);
        return Result.Ok<IReadOnlyList<ApplicationDto>>(rows.Select(a => a.ToDto()).ToList());
    }

    public async Task<Result<ApplicationDto>> ChangeStatusAsync(Guid id, ApplicationStatus status, CancellationToken ct = default)
    {
        var app = await _db.Applications
            .Include(a => a.Job)!.ThenInclude(j => j!.Company)
            .Include(a => a.Worker)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app is null || app.Job is null) return Result.Fail<ApplicationDto>("Application not found.", ErrorCode.NotFound);

        var isAdmin = _currentUser.Role == UserRole.Admin;
        var isOwner = _currentUser.Role == UserRole.Employer && _currentUser.CompanyId == app.Job.CompanyId;
        var isApplicant = _currentUser.Role == UserRole.Worker && _currentUser.UserId == app.WorkerId;

        // Workers can only withdraw their own applications. Employers/admins can do everything else.
        if (status == ApplicationStatus.Withdrawn)
        {
            if (!isApplicant && !isAdmin)
                return Result.Fail<ApplicationDto>("Only the applicant or an admin can withdraw an application.", ErrorCode.Forbidden);
        }
        else if (!isOwner && !isAdmin)
        {
            return Result.Fail<ApplicationDto>("You can't change this application's status.", ErrorCode.Forbidden);
        }

        app.Status = status;

        if (status == ApplicationStatus.Selected)
        {
            // Hire workflow: mark job as Filled, auto-reject any other open applicants.
            app.Job.Status = JobStatus.Filled;

            var others = await _db.Applications
                .Where(a => a.JobId == app.JobId && a.Id != app.Id
                            && a.Status != ApplicationStatus.Rejected
                            && a.Status != ApplicationStatus.Withdrawn)
                .ToListAsync(ct);

            foreach (var o in others)
            {
                o.Status = ApplicationStatus.Rejected;
                _notify.Enqueue(o.WorkerId, NotificationType.Status,
                    "Application update",
                    $"Another candidate was selected for \"{app.Job.Title}\". Thanks for applying.",
                    "/worker/applications");
            }

            _notify.Enqueue(app.WorkerId, NotificationType.Status,
                "You have been selected!",
                $"You were selected for \"{app.Job.Title}\". Please coordinate next steps with the employer.",
                "/worker/applications");

            await _audit.PublishEventAsync("CandidateSelected", "Application", app.Id.ToString(),
                new Dictionary<string, object?>
                {
                    ["jobId"] = app.JobId,
                    ["workerId"] = app.WorkerId,
                    ["otherApplicantsRejected"] = others.Count,
                }, ct);
        }
        else if (status == ApplicationStatus.Shortlisted)
        {
            _notify.Enqueue(app.WorkerId, NotificationType.Status,
                "You have been shortlisted",
                $"You were shortlisted for \"{app.Job.Title}\". The employer may reach out soon.",
                "/worker/applications");
        }
        else if (status == ApplicationStatus.Rejected)
        {
            _notify.Enqueue(app.WorkerId, NotificationType.Status,
                "Application update",
                $"Your application for \"{app.Job.Title}\" was not selected this time.",
                "/worker/applications");
        }
        else if (status == ApplicationStatus.Withdrawn && app.Job.Company is not null)
        {
            _notify.Enqueue(app.Job.Company.OwnerId, NotificationType.Status,
                "Application withdrawn",
                $"{app.Worker?.FirstName} {app.Worker?.LastName} withdrew from \"{app.Job.Title}\".",
                $"/employer/jobs/{app.Job.Id}");
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync($"Application.Status.{status}", "Application", app.Id.ToString(), ct: ct);

        return Result.Ok(await BuildDtoAsync(app.Id, ct));
    }

    private async Task<ApplicationDto> BuildDtoAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.Applications
            .Include(x => x.Job)!.ThenInclude(j => j!.Company)
            .Include(x => x.Worker)!.ThenInclude(w => w!.WorkerProfile)
            .AsNoTracking()
            .FirstAsync(x => x.Id == id, ct);
        return a.ToDto();
    }
}
