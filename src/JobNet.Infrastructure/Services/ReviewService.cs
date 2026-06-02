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

public interface IReviewService
{
    Task<Result<ReviewDto>> CreateAsync(CreateReviewRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewDto>> ListForCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewDto>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewDto>> ListByAuthorAsync(Guid userId, CancellationToken ct = default);
}

public class ReviewService : IReviewService
{
    private readonly JobNetDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly INotificationService _notify;

    public ReviewService(JobNetDbContext db, ICurrentUser currentUser, IAuditLogger audit, INotificationService notify)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notify = notify;
    }

    public async Task<Result<ReviewDto>> CreateAsync(CreateReviewRequest req, CancellationToken ct = default)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Fail<ReviewDto>("You must be signed in to leave a review.", ErrorCode.Unauthorized);
        if (req.Rating < 1 || req.Rating > 5)
            return Result.Fail<ReviewDto>("Rating must be between 1 and 5.", ErrorCode.Validation);
        if (string.IsNullOrWhiteSpace(req.Comment))
            return Result.Fail<ReviewDto>("Comment is required.", ErrorCode.Validation);
        if ((req.ToUserId is null && req.ToCompanyId is null) || (req.ToUserId is not null && req.ToCompanyId is not null))
            return Result.Fail<ReviewDto>("A review must target either a user or a company (not both).", ErrorCode.Validation);

        var fromId = _currentUser.UserId.Value;

        // Workflow rule: only allow reviews tied to a selected application.
        if (req.JobId.HasValue)
        {
            var sel = await _db.Applications.FirstOrDefaultAsync(a => a.JobId == req.JobId.Value && a.Status == ApplicationStatus.Selected, ct);
            if (sel is null)
                return Result.Fail<ReviewDto>("Reviews are only allowed after a candidate is selected on this job.", ErrorCode.Validation);
        }

        // Prevent duplicate reviews from the same author for the same target + job.
        var exists = await _db.Reviews.AnyAsync(r =>
            r.FromUserId == fromId &&
            r.JobId == req.JobId &&
            r.ToUserId == req.ToUserId &&
            r.ToCompanyId == req.ToCompanyId, ct);
        if (exists)
            return Result.Fail<ReviewDto>("You've already submitted a review for this.", ErrorCode.Conflict);

        var review = new Review
        {
            FromUserId = fromId,
            ToUserId = req.ToUserId,
            ToCompanyId = req.ToCompanyId,
            JobId = req.JobId,
            Rating = req.Rating,
            Comment = req.Comment.Trim(),
        };
        _db.Reviews.Add(review);

        // Recompute aggregate rating for the target.
        if (req.ToCompanyId.HasValue)
        {
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == req.ToCompanyId.Value, ct);
            if (company is not null)
            {
                var stats = await _db.Reviews
                    .Where(r => r.ToCompanyId == company.Id)
                    .GroupBy(r => 1)
                    .Select(g => new { Count = g.Count() + 1, Sum = g.Sum(r => r.Rating) + req.Rating })
                    .FirstOrDefaultAsync(ct);
                if (stats is null) { company.Rating = req.Rating; company.ReviewCount = 1; }
                else { company.Rating = Math.Round((double)stats.Sum / stats.Count, 2); company.ReviewCount = stats.Count; }
                _notify.Enqueue(company.OwnerId, NotificationType.Review,
                    "New review on your company",
                    $"You received a new {req.Rating}-star review.",
                    "/employer/reviews");
            }
        }
        if (req.ToUserId.HasValue)
        {
            var profile = await _db.WorkerProfiles.FirstOrDefaultAsync(p => p.UserId == req.ToUserId.Value, ct);
            if (profile is not null)
            {
                var stats = await _db.Reviews
                    .Where(r => r.ToUserId == profile.UserId)
                    .GroupBy(r => 1)
                    .Select(g => new { Count = g.Count() + 1, Sum = g.Sum(r => r.Rating) + req.Rating })
                    .FirstOrDefaultAsync(ct);
                if (stats is null) { profile.Rating = req.Rating; profile.ReviewCount = 1; }
                else { profile.Rating = Math.Round((double)stats.Sum / stats.Count, 2); profile.ReviewCount = stats.Count; }
                _notify.Enqueue(profile.UserId, NotificationType.Review,
                    "You received a new review",
                    $"You received a new {req.Rating}-star review.",
                    "/worker/profile");
            }
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Review.Created", "Review", review.Id.ToString(), ct: ct);

        var saved = await _db.Reviews
            .Include(r => r.FromUser).Include(r => r.Job)
            .AsNoTracking().FirstAsync(r => r.Id == review.Id, ct);
        return Result.Ok(saved.ToDto());
    }

    public async Task<IReadOnlyList<ReviewDto>> ListForCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        var rows = await _db.Reviews
            .Where(r => r.ToCompanyId == companyId)
            .Include(r => r.FromUser).Include(r => r.Job)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<ReviewDto>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.Reviews
            .Where(r => r.ToUserId == userId)
            .Include(r => r.FromUser).Include(r => r.Job)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<ReviewDto>> ListByAuthorAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.Reviews
            .Where(r => r.FromUserId == userId)
            .Include(r => r.FromUser).Include(r => r.Job)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }
}
