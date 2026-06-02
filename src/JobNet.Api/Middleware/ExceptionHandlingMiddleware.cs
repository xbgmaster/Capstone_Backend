using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace JobNet.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            if (context.Response.HasStarted) return;
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Title = "InternalServerError",
                Detail = "An unexpected error occurred. Please try again.",
                Status = 500,
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
    }
}
