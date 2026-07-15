namespace JobNet.Infrastructure.Email;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlContent, CancellationToken ct = default);
}
