namespace JobNet.Infrastructure.Auth;

public class JwtSettings
{
    public string Issuer { get; set; } = "JobNet";
    public string Audience { get; set; } = "JobNet.Client";
    public string SigningKey { get; set; } = "change-me-to-a-long-random-secret-at-least-32-chars";
    public int ExpiresMinutes { get; set; } = 60 * 8; // 8 hours
}
