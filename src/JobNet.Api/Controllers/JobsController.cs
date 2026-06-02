using JobNet.Api.Middleware;
using JobNet.Domain.Enums;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
    private readonly IJobService _jobs;
    private readonly IApplicationService _applications;

    public JobsController(IJobService jobs, IApplicationService applications)
    {
        _jobs = jobs;
        _applications = applications;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<JobDto>>> List(
        [FromQuery] string? query,
        [FromQuery] string? category,
        [FromQuery] string? province,
        [FromQuery] bool? onlyOpen,
        [FromQuery] Guid? companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var filter = new JobFilter(query, category, province, onlyOpen, companyId, page, pageSize);
        return Ok(await _jobs.ListAsync(filter, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobDto>> Get(Guid id, CancellationToken ct)
        => (await _jobs.GetAsync(id, ct)).ToActionResult();

    [Authorize(Roles = nameof(UserRole.Employer))]
    [HttpPost]
    public async Task<ActionResult<JobDto>> Create([FromBody] CreateJobRequest req, CancellationToken ct)
        => (await _jobs.CreateAsync(req, ct)).ToActionResult();

    [Authorize(Roles = nameof(UserRole.Employer) + "," + nameof(UserRole.Admin))]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JobDto>> Update(Guid id, [FromBody] UpdateJobRequest req, CancellationToken ct)
        => (await _jobs.UpdateAsync(id, req, ct)).ToActionResult();

    [Authorize(Roles = nameof(UserRole.Employer) + "," + nameof(UserRole.Admin))]
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<JobDto>> ChangeStatus(Guid id, [FromBody] ChangeJobStatusRequest req, CancellationToken ct)
        => (await _jobs.ChangeStatusAsync(id, req.Status, ct)).ToActionResult();

    [Authorize(Roles = nameof(UserRole.Employer) + "," + nameof(UserRole.Admin))]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
        => (await _jobs.DeleteAsync(id, ct)).ToActionResult();

    [Authorize(Roles = nameof(UserRole.Employer) + "," + nameof(UserRole.Admin))]
    [HttpGet("{id:guid}/applications")]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> ListApplications(Guid id, CancellationToken ct)
        => (await _applications.ListForJobAsync(id, ct)).ToActionResult();
}
