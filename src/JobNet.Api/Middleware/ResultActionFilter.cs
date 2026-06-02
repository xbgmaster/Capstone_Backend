using JobNet.Infrastructure.Common;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Middleware;

/// <summary>
/// Helper methods to map our service `Result`/`Result&lt;T&gt;` to standard ActionResult responses.
/// </summary>
public static class ResultExtensions
{
    public static ActionResult ToActionResult(this Result result)
    {
        if (result.Success) return new NoContentResult();
        return MapError(result);
    }

    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        if (result.Success) return new OkObjectResult(result.Value);
        return MapError(result);
    }

    private static ActionResult MapError(Result r)
    {
        var problem = new ProblemDetails
        {
            Title = r.Code.ToString(),
            Detail = r.Error,
            Status = r.Code switch
            {
                ErrorCode.NotFound => StatusCodes.Status404NotFound,
                ErrorCode.Validation => StatusCodes.Status400BadRequest,
                ErrorCode.Conflict => StatusCodes.Status409Conflict,
                ErrorCode.Forbidden => StatusCodes.Status403Forbidden,
                ErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError,
            },
        };
        return new ObjectResult(problem) { StatusCode = problem.Status };
    }
}
