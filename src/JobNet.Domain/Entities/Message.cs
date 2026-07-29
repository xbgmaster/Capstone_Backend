namespace JobNet.Domain.Entities;

/// <summary>
/// A direct message between two users (typically a worker and an employer).
/// Optionally scoped to a Job so each application can have its own thread.
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SenderId { get; set; }
    public User? Sender { get; set; }

    public Guid RecipientId { get; set; }
    public User? Recipient { get; set; }

    // Optional: ties the conversation to a specific job/application.
    public Guid? JobId { get; set; }
    public Job? Job { get; set; }

    public string Body { get; set; } = string.Empty;
    public bool Read { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
