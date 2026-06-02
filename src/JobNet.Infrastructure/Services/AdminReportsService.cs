using JobNet.Domain.Enums;
using JobNet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobNet.Infrastructure.Services;

public record OverviewStats(
    int TotalUsers,
    int Workers,
    int Employers,
    int Companies,
    int OpenJobs,
    int Applications,
    int SelectedHires,
    int Reviews
);

public record BarSlice(string Label, int Value);

public record FunnelStats(
    IReadOnlyList<BarSlice> JobsByCategory,
    IReadOnlyList<BarSlice> JobsByProvince,
    IReadOnlyList<BarSlice> ApplicationFunnel,
    int ConversionPercent
);

public interface IAdminReportsService
{
    Task<OverviewStats> GetOverviewAsync(CancellationToken ct = default);
    Task<FunnelStats> GetFunnelAsync(CancellationToken ct = default);
}

public class AdminReportsService : IAdminReportsService
{
    private readonly JobNetDbContext _db;

    public AdminReportsService(JobNetDbContext db)
    {
        _db = db;
    }

    public async Task<OverviewStats> GetOverviewAsync(CancellationToken ct = default)
    {
        var users = await _db.Users.GroupBy(u => u.Role).Select(g => new { Role = g.Key, Count = g.Count() }).ToListAsync(ct);
        int Get(UserRole r) => users.FirstOrDefault(x => x.Role == r)?.Count ?? 0;

        return new OverviewStats(
            TotalUsers: users.Sum(u => u.Count),
            Workers: Get(UserRole.Worker),
            Employers: Get(UserRole.Employer),
            Companies: await _db.Companies.CountAsync(ct),
            OpenJobs: await _db.Jobs.CountAsync(j => j.Status == JobStatus.Open, ct),
            Applications: await _db.Applications.CountAsync(ct),
            SelectedHires: await _db.Applications.CountAsync(a => a.Status == ApplicationStatus.Selected, ct),
            Reviews: await _db.Reviews.CountAsync(ct)
        );
    }

    public async Task<FunnelStats> GetFunnelAsync(CancellationToken ct = default)
    {
        var byCategory = await _db.Jobs
            .GroupBy(j => j.Category)
            .Select(g => new BarSlice(g.Key, g.Count()))
            .ToListAsync(ct);

        // SQL Server doesn't translate "split last token", so do province extraction client-side.
        var locations = await _db.Jobs.Select(j => j.Location).ToListAsync(ct);
        var byProvince = locations
            .Select(l => l.Split(',').LastOrDefault()?.Trim() ?? "Unknown")
            .GroupBy(p => p)
            .Select(g => new BarSlice(g.Key, g.Count()))
            .OrderByDescending(s => s.Value)
            .ToList();

        var byStatus = await _db.Applications
            .GroupBy(a => a.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var statusOrder = new[]
        {
            ApplicationStatus.Submitted, ApplicationStatus.Shortlisted, ApplicationStatus.Selected,
            ApplicationStatus.Rejected, ApplicationStatus.Withdrawn
        };
        var funnel = statusOrder
            .Select(s => new BarSlice(s.ToString(), byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0))
            .ToList();

        var totalApps = byStatus.Sum(x => x.Count);
        var selected = byStatus.FirstOrDefault(x => x.Key == ApplicationStatus.Selected)?.Count ?? 0;
        var conversion = totalApps == 0 ? 0 : (int)Math.Round(selected * 100.0 / totalApps);

        return new FunnelStats(byCategory, byProvince, funnel, conversion);
    }
}
