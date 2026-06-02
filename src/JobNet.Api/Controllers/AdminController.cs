using JobNet.Domain.Enums;
using JobNet.Infrastructure.Persistence.Seeding;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminController : ControllerBase
{
    private readonly IAdminReportsService _reports;
    private readonly DatabaseSeeder _seeder;

    public AdminController(IAdminReportsService reports, DatabaseSeeder seeder)
    {
        _reports = reports;
        _seeder = seeder;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<OverviewStats>> Overview(CancellationToken ct)
        => Ok(await _reports.GetOverviewAsync(ct));

    [HttpGet("reports/funnel")]
    public async Task<ActionResult<FunnelStats>> Funnel(CancellationToken ct)
        => Ok(await _reports.GetFunnelAsync(ct));

    /// <summary>
    /// Idempotent seed: populates demo data only if the database is empty.
    /// </summary>
    [HttpPost("seed")]
    public async Task<ActionResult> Seed(CancellationToken ct)
    {
        await _seeder.SeedAsync(ct);
        return Ok(new { ok = true });
    }

    /// <summary>
    /// Destructive: wipes all business data and re-seeds. Used by the admin
    /// panel "Reset all data" button.
    /// </summary>
    [HttpPost("reset")]
    public async Task<ActionResult> Reset(CancellationToken ct)
    {
        await _seeder.ResetAsync(ct);
        return Ok(new { ok = true });
    }
}
