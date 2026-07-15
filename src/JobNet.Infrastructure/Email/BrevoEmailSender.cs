using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobNet.Infrastructure.Email;

/// <summary>
/// Sends transactional emails through Brevo's REST API (https://api.brevo.com/v3/smtp/email).
/// </summary>
public class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly BrevoSettings _settings;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(HttpClient http, IOptions<BrevoSettings> settings, ILogger<BrevoEmailSender> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlContent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new InvalidOperationException("Brevo ApiKey is not configured (set Brevo:ApiKey).");
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail))
            throw new InvalidOperationException("Brevo SenderEmail is not configured (set Brevo:SenderEmail).");

        var payload = new BrevoEmailRequest
        {
            Sender = new BrevoContact { Name = _settings.SenderName, Email = _settings.SenderEmail },
            To = new[] { new BrevoContact { Name = string.IsNullOrWhiteSpace(toName) ? toEmail : toName, Email = toEmail } },
            Subject = subject,
            HtmlContent = htmlContent,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("api-key", _settings.ApiKey);
        request.Headers.Add("accept", "application/json");

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Brevo email send failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Brevo email send failed with status {(int)response.StatusCode}.");
        }
    }

    private sealed class BrevoEmailRequest
    {
        [JsonPropertyName("sender")] public BrevoContact Sender { get; set; } = new();
        [JsonPropertyName("to")] public BrevoContact[] To { get; set; } = Array.Empty<BrevoContact>();
        [JsonPropertyName("subject")] public string Subject { get; set; } = string.Empty;
        [JsonPropertyName("htmlContent")] public string HtmlContent { get; set; } = string.Empty;
    }

    private sealed class BrevoContact
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    }
}
