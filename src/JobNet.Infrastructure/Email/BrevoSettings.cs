namespace JobNet.Infrastructure.Email;

public class BrevoSettings
{
    /// <summary>Brevo (Sendinblue) transactional API key ("xkeysib-...").</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Display name shown as the sender.</summary>
    public string SenderName { get; set; } = "Jobnet";

    /// <summary>Verified sender email registered in your Brevo account.</summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>Base URL of the frontend, used to build the password reset link.</summary>
    public string AppBaseUrl { get; set; } = "http://localhost:5173";
}
