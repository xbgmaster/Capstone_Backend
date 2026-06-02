namespace JobNet.Domain.Entities;

public class WorkerProfile
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string? Headline { get; set; }
    public string? Bio { get; set; }
    public int YearsExperience { get; set; }
    public decimal HourlyRate { get; set; }
    public string Availability { get; set; } = "Flexible";

    public double Rating { get; set; }
    public int ReviewCount { get; set; }

    public ICollection<WorkerSkill> Skills { get; set; } = new List<WorkerSkill>();
    public ICollection<Certification> Certifications { get; set; } = new List<Certification>();
    public ICollection<Experience> Experiences { get; set; } = new List<Experience>();
}

public class WorkerSkill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkerId { get; set; }
    public WorkerProfile? Worker { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Certification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkerId { get; set; }
    public WorkerProfile? Worker { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public int Year { get; set; }
}

public class Experience
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkerId { get; set; }
    public WorkerProfile? Worker { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}
