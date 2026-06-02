using JobNet.Api.Middleware;
using JobNet.Infrastructure.Auth;
using JobNet.Infrastructure.Contracts;
using JobNet.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviews;
    private readonly ICurrentUser _currentUser;

    public ReviewsController(IReviewService reviews, ICurrentUser currentUser)
    {
        _reviews = reviews;
        _currentUser = currentUser;
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ReviewDto>> Create([FromBody] CreateReviewRequest req, CancellationToken ct)
        => (await _reviews.CreateAsync(req, ct)).ToActionResult();

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> List(
        [FromQuery] Guid? toCompanyId,
        [FromQuery] Guid? toUserId,
        [FromQuery] Guid? authoredBy,
        CancellationToken ct)
    {
        if (toCompanyId.HasValue) return Ok(await _reviews.ListForCompanyAsync(toCompanyId.Value, ct));
        if (toUserId.HasValue) return Ok(await _reviews.ListForUserAsync(toUserId.Value, ct));
        if (authoredBy.HasValue) return Ok(await _reviews.ListByAuthorAsync(authoredBy.Value, ct));
        return BadRequest(new ProblemDetails
        {
            Title = "Validation",
            Detail = "Specify one of toCompanyId, toUserId, or authoredBy.",
            Status = 400,
        });
    }
}
