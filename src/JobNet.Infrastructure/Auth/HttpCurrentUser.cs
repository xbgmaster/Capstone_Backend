using System.Security.Claims;
using JobNet.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace JobNet.Infrastructure.Auth;

public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public UserRole? Role
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(raw, out var r) ? r : null;
        }
    }

    public Guid? CompanyId
    {
        get
        {
            var raw = Principal?.FindFirstValue("companyId");
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent =>
        _accessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
