using JobNet.Api.Middleware;
using JobNet.Domain.Enums;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Controllers;

[ApiController]
[Route("api/companies")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companies;

    public CompaniesController(ICompanyService companies)
    {
        _companies = companies;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> List([FromQuery] string? query, CancellationToken ct)
        => Ok(await _companies.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> Get(Guid id, CancellationToken ct)
        => (await _companies.GetAsync(id, ct)).ToActionResult();

    [Authorize(Roles = nameof(UserRole.Employer) + "," + nameof(UserRole.Admin))]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> Update(Guid id, [FromBody] UpdateCompanyRequest req, CancellationToken ct)
        => (await _companies.UpdateAsync(id, req, ct)).ToActionResult();

    public record VerifyRequest(bool Verified);

    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPatch("{id:guid}/verify")]
    public async Task<ActionResult<CompanyDto>> Verify(Guid id, [FromBody] VerifyRequest req, CancellationToken ct)
        => (await _companies.SetVerifiedAsync(id, req.Verified, ct)).ToActionResult();
}
