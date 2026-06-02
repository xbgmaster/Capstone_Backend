using JobNet.Domain.Entities;
using JobNet.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobNet.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the same demo data the React frontend used to ship with as its in-memory store.
/// Idempotent: only seeds when the Users table is empty.
/// </summary>
public class DatabaseSeeder
{
    private readonly JobNetDbContext _db;
    private readonly ILogger<DatabaseSeeder> _log;

    public DatabaseSeeder(JobNetDbContext db, ILogger<DatabaseSeeder> log)
    {
        _db = db;
        _log = log;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(ct))
        {
            _log.LogInformation("Database already seeded; skipping.");
            return;
        }

        await SeedInternalAsync(ct);
    }

    /// <summary>
    /// Wipes every business table and re-seeds. Used by the admin panel's
    /// "Reset all data" button. The MongoDB audit log is intentionally left
    /// untouched so the reset itself is auditable.
    /// </summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        _log.LogWarning("Resetting all business data...");

        // Order matters: child tables first to satisfy FKs.
        _db.Notifications.RemoveRange(_db.Notifications);
        _db.Reviews.RemoveRange(_db.Reviews);
        _db.Applications.RemoveRange(_db.Applications);
        _db.JobSkills.RemoveRange(_db.JobSkills);
        _db.Jobs.RemoveRange(_db.Jobs);
        _db.WorkerSkills.RemoveRange(_db.WorkerSkills);
        _db.Certifications.RemoveRange(_db.Certifications);
        _db.Experiences.RemoveRange(_db.Experiences);
        _db.WorkerProfiles.RemoveRange(_db.WorkerProfiles);

        // Detach Users from Companies to break the FK cycle before deleting.
        foreach (var u in _db.Users) u.CompanyId = null;
        await _db.SaveChangesAsync(ct);

        _db.Companies.RemoveRange(_db.Companies);
        _db.Users.RemoveRange(_db.Users);
        await _db.SaveChangesAsync(ct);

        await SeedInternalAsync(ct);
    }

    private async Task SeedInternalAsync(CancellationToken ct)
    {
        _log.LogInformation("Seeding database with demo data...");

        // ---- Users ----
        var admin = NewUser("u-admin-1", UserRole.Admin, "Sarah", "Mitchell", "admin@jobnet.ca", "admin123",
            "+1 (604) 555-0001", "Vancouver", "BC", "SM");
        var emp1 = NewUser("u-emp-1", UserRole.Employer, "David", "Chen", "david@northbuild.ca", "employer123",
            "+1 (416) 555-0123", "Toronto", "ON", "DC");
        var emp2 = NewUser("u-emp-2", UserRole.Employer, "Emma", "Tremblay", "emma@maplecontractors.ca", "employer123",
            "+1 (514) 555-0188", "Montreal", "QC", "ET");
        var wrk1 = NewUser("u-wrk-1", UserRole.Worker, "Marcus", "Johnson", "marcus@example.com", "worker123",
            "+1 (403) 555-0177", "Calgary", "AB", "MJ");
        var wrk2 = NewUser("u-wrk-2", UserRole.Worker, "Priya", "Sharma", "priya@example.com", "worker123",
            "+1 (647) 555-0145", "Toronto", "ON", "PS");
        var wrk3 = NewUser("u-wrk-3", UserRole.Worker, "Liam", "O'Brien", "liam@example.com", "worker123",
            "+1 (902) 555-0199", "Halifax", "NS", "LO");

        // Users go in first WITHOUT a CompanyId; we attach companies below.
        // This breaks the Users<->Companies circular FK that would otherwise
        // confuse EF's topological sort on insert.
        _db.Users.AddRange(admin, emp1, emp2, wrk1, wrk2, wrk3);
        await _db.SaveChangesAsync(ct);

        // ---- Companies ----
        var c1 = new Company
        {
            Id = StableGuid("c-1"),
            OwnerId = emp1.Id,
            Name = "NorthBuild Construction Inc.",
            Industry = "Commercial Construction",
            BusinessNumber = "123456789RC0001",
            Website = "https://northbuild.ca",
            Email = "contact@northbuild.ca",
            Phone = "+1 (416) 555-0100",
            Address = "120 King Street West, Toronto, ON",
            City = "Toronto",
            Province = "ON",
            FoundedYear = 2008,
            EmployeeCount = "50-200",
            Description = "NorthBuild is a leading commercial construction firm in the GTA, specializing in office buildings, retail spaces, and mixed-use developments.",
            Rating = 4.6,
            ReviewCount = 32,
            Verified = true,
        };
        var c2 = new Company
        {
            Id = StableGuid("c-2"),
            OwnerId = emp2.Id,
            Name = "Maple Contractors Ltd.",
            Industry = "Residential & Renovations",
            BusinessNumber = "987654321RC0001",
            Website = "https://maplecontractors.ca",
            Email = "hello@maplecontractors.ca",
            Phone = "+1 (514) 555-0100",
            Address = "450 Rue Saint-Jacques, Montreal, QC",
            City = "Montreal",
            Province = "QC",
            FoundedYear = 2014,
            EmployeeCount = "10-50",
            Description = "Family-run residential renovation specialists serving the Greater Montreal area for over a decade.",
            Rating = 4.3,
            ReviewCount = 18,
            Verified = true,
        };
        _db.Companies.AddRange(c1, c2);
        await _db.SaveChangesAsync(ct);

        // Now that Companies exist, attach employer users to them.
        emp1.CompanyId = c1.Id;
        emp2.CompanyId = c2.Id;
        await _db.SaveChangesAsync(ct);

        // ---- Worker Profiles ----
        var p1 = new WorkerProfile
        {
            UserId = wrk1.Id,
            Headline = "Journeyman Electrician - Red Seal Certified",
            Bio = "Red Seal certified electrician with 8 years of experience in commercial and industrial wiring. Available for short and long-term contracts across Alberta.",
            YearsExperience = 8,
            HourlyRate = 55m,
            Availability = "Full-time",
            Rating = 4.8,
            ReviewCount = 14,
            Skills = SkillList(new[] { "Electrical", "Commercial Wiring", "Conduit Bending", "Blueprint Reading", "Troubleshooting" }),
            Certifications = new List<Certification>
            {
                new() { Name = "Red Seal - Construction Electrician", Issuer = "Government of Canada", Year = 2019 },
                new() { Name = "Working at Heights", Issuer = "Alberta OHS", Year = 2024 },
            },
            Experiences = new List<Experience>
            {
                new() { Title = "Lead Electrician", Company = "BrightSpark Electric", From = "2022", To = "Present" },
                new() { Title = "Journeyman Electrician", Company = "PowerLine Services", From = "2018", To = "2022" },
            },
        };
        var p2 = new WorkerProfile
        {
            UserId = wrk2.Id,
            Headline = "Interior Painter & Drywall Finisher",
            Bio = "Detail-oriented painter and drywall finisher with experience in high-end residential renovations.",
            YearsExperience = 5,
            HourlyRate = 38m,
            Availability = "Part-time",
            Rating = 4.5,
            ReviewCount = 9,
            Skills = SkillList(new[] { "Painting", "Drywall", "Taping & Mudding", "Wallpaper", "Surface Prep" }),
            Certifications = new List<Certification>
            {
                new() { Name = "WHMIS 2015", Issuer = "CCOHS", Year = 2023 },
            },
            Experiences = new List<Experience>
            {
                new() { Title = "Senior Painter", Company = "Freelance", From = "2021", To = "Present" },
                new() { Title = "Painter", Company = "Crisp Coats Inc.", From = "2019", To = "2021" },
            },
        };
        var p3 = new WorkerProfile
        {
            UserId = wrk3.Id,
            Headline = "General Labourer / Site Helper",
            Bio = "Reliable site labourer comfortable with demolition, material handling, and clean-up. Available for short-term gigs across the Maritimes.",
            YearsExperience = 2,
            HourlyRate = 24m,
            Availability = "Flexible",
            Rating = 4.2,
            ReviewCount = 4,
            Skills = SkillList(new[] { "Demolition", "Material Handling", "Site Clean-up", "Hand Tools" }),
            Certifications = new List<Certification>
            {
                new() { Name = "WHMIS 2015", Issuer = "CCOHS", Year = 2025 },
            },
            Experiences = new List<Experience>
            {
                new() { Title = "General Labourer", Company = "Atlantic Build Co.", From = "2024", To = "Present" },
            },
        };
        _db.WorkerProfiles.AddRange(p1, p2, p3);

        // ---- Jobs ----
        var j1 = new Job
        {
            Id = StableGuid("j-1"),
            CompanyId = c1.Id,
            Title = "Commercial Electrician - Downtown Tower Project",
            Category = "Electrical",
            Description = "We are hiring 3 commercial electricians for a 12-month tower fit-out in downtown Toronto. You will install conduit, pull wire, and terminate panels under the supervision of a master electrician.",
            Activity = "Day shifts Monday-Friday, occasional Saturdays. Tools provided on site; PPE required.",
            Location = "Toronto, ON",
            DueDate = new DateTime(2026, 8, 31),
            PaymentType = PaymentType.Hourly,
            PaymentAmount = 52m,
            Status = JobStatus.Open,
            PostedAt = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc),
            SkillsRequired = JobSkillList(new[] { "Commercial Wiring", "Conduit Bending", "Blueprint Reading" }),
        };
        var j2 = new Job
        {
            Id = StableGuid("j-2"),
            CompanyId = c1.Id,
            Title = "Site Supervisor - Retail Renovation",
            Category = "Supervision",
            Description = "Site supervisor needed for an 8-week retail renovation in Mississauga. Coordinate trades, manage timelines, and ensure safety compliance.",
            Activity = "Full-time, 8 weeks",
            Location = "Mississauga, ON",
            DueDate = new DateTime(2026, 7, 15),
            PaymentType = PaymentType.Fixed,
            PaymentAmount = 18000m,
            Status = JobStatus.Open,
            PostedAt = new DateTime(2026, 5, 14, 14, 30, 0, DateTimeKind.Utc),
            SkillsRequired = JobSkillList(new[] { "Supervision", "Scheduling", "Safety Compliance" }),
        };
        var j3 = new Job
        {
            Id = StableGuid("j-3"),
            CompanyId = c2.Id,
            Title = "Interior Painter - Plateau Condo Refresh",
            Category = "Painting",
            Description = "Paint 12 condo units in the Plateau. Surfaces are prepped. We supply paint and materials - you bring brushes, rollers, and drop sheets.",
            Activity = "3-week contract starting June 1",
            Location = "Montreal, QC",
            DueDate = new DateTime(2026, 6, 21),
            PaymentType = PaymentType.Fixed,
            PaymentAmount = 6400m,
            Status = JobStatus.Open,
            PostedAt = new DateTime(2026, 5, 12, 9, 15, 0, DateTimeKind.Utc),
            SkillsRequired = JobSkillList(new[] { "Painting", "Surface Prep" }),
        };
        var j4 = new Job
        {
            Id = StableGuid("j-4"),
            CompanyId = c2.Id,
            Title = "General Labourer - Demolition Crew",
            Category = "General Labour",
            Description = "Two general labourers needed for a kitchen and bathroom demolition in Westmount. Must be comfortable with manual labour and lifting.",
            Activity = "1-week project, Mon-Fri 8-4",
            Location = "Westmount, QC",
            DueDate = new DateTime(2026, 6, 7),
            PaymentType = PaymentType.Hourly,
            PaymentAmount = 26m,
            Status = JobStatus.Open,
            PostedAt = new DateTime(2026, 5, 18, 8, 0, 0, DateTimeKind.Utc),
            SkillsRequired = JobSkillList(new[] { "Demolition", "Hand Tools", "Material Handling" }),
        };
        var j5 = new Job
        {
            Id = StableGuid("j-5"),
            CompanyId = c1.Id,
            Title = "HVAC Technician - Office Retrofit",
            Category = "HVAC",
            Description = "Licensed HVAC technician needed for a 6-week office retrofit. Install rooftop units and ductwork.",
            Activity = "6 weeks, full-time",
            Location = "Toronto, ON",
            DueDate = new DateTime(2026, 7, 30),
            PaymentType = PaymentType.Hourly,
            PaymentAmount = 48m,
            Status = JobStatus.Closed,
            PostedAt = new DateTime(2026, 4, 22, 11, 0, 0, DateTimeKind.Utc),
            SkillsRequired = JobSkillList(new[] { "HVAC", "Ductwork", "Sheet Metal" }),
        };
        _db.Jobs.AddRange(j1, j2, j3, j4, j5);

        // ---- Applications ----
        _db.Applications.AddRange(
            new Application { JobId = j1.Id, WorkerId = wrk1.Id, CoverLetter = "I have 8 years of commercial wiring experience including two recent tower projects in Calgary. Available immediately.", ExpectedRate = 55m, Status = ApplicationStatus.Submitted, SubmittedAt = new DateTime(2026, 5, 11, 13, 20, 0, DateTimeKind.Utc) },
            new Application { JobId = j3.Id, WorkerId = wrk2.Id, CoverLetter = "I have completed similar condo refreshes in the Plateau and can start June 1. References available on request.", ExpectedRate = 38m, Status = ApplicationStatus.Shortlisted, SubmittedAt = new DateTime(2026, 5, 13, 10, 45, 0, DateTimeKind.Utc) },
            new Application { JobId = j4.Id, WorkerId = wrk3.Id, CoverLetter = "Available for the full week. Comfortable with demolition and clean-up.", ExpectedRate = 26m, Status = ApplicationStatus.Submitted, SubmittedAt = new DateTime(2026, 5, 19, 7, 50, 0, DateTimeKind.Utc) },
            new Application { JobId = j5.Id, WorkerId = wrk1.Id, CoverLetter = "Cross-trained in HVAC controls and rooftop unit installs.", ExpectedRate = 50m, Status = ApplicationStatus.Rejected, SubmittedAt = new DateTime(2026, 4, 25, 15, 10, 0, DateTimeKind.Utc) }
        );

        // ---- Reviews ----
        _db.Reviews.AddRange(
            new Review { FromUserId = emp1.Id, ToUserId = wrk1.Id, JobId = j5.Id, Rating = 5, Comment = "Marcus did excellent work on our last retrofit. Highly recommended.", CreatedAt = new DateTime(2026, 4, 30, 16, 0, 0, DateTimeKind.Utc) },
            new Review { FromUserId = wrk1.Id, ToCompanyId = c1.Id, JobId = j5.Id, Rating = 5, Comment = "Clear scope, on-time payments, professional site management.", CreatedAt = new DateTime(2026, 4, 30, 17, 0, 0, DateTimeKind.Utc) }
        );

        // ---- Notifications ----
        _db.Notifications.AddRange(
            new Notification { UserId = emp1.Id, Type = NotificationType.Application, Title = "New application", Message = "Marcus Johnson applied to \"Commercial Electrician - Downtown Tower Project\".", Link = "/employer/jobs/" + j1.Id, Read = false, CreatedAt = new DateTime(2026, 5, 11, 13, 20, 0, DateTimeKind.Utc) },
            new Notification { UserId = wrk2.Id, Type = NotificationType.Status, Title = "You have been shortlisted", Message = "Maple Contractors shortlisted you for \"Interior Painter - Plateau Condo Refresh\".", Link = "/worker/applications", Read = false, CreatedAt = new DateTime(2026, 5, 15, 9, 0, 0, DateTimeKind.Utc) },
            new Notification { UserId = wrk1.Id, Type = NotificationType.Status, Title = "Application update", Message = "Your application for \"HVAC Technician - Office Retrofit\" was not selected.", Link = "/worker/applications", Read = true, CreatedAt = new DateTime(2026, 4, 28, 11, 30, 0, DateTimeKind.Utc) }
        );

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Seed complete: {Users} users, {Companies} companies, {Jobs} jobs.",
            6, 2, 5);
    }

    private static User NewUser(string stableKey, UserRole role, string first, string last, string email, string password,
        string phone, string city, string province, string avatar)
    {
        return new User
        {
            Id = StableGuid(stableKey),
            Role = role,
            FirstName = first,
            LastName = last,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Phone = phone,
            City = city,
            Province = province,
            Avatar = avatar,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
    }

    private static List<WorkerSkill> SkillList(IEnumerable<string> names)
        => names.Select(n => new WorkerSkill { Name = n }).ToList();

    private static List<JobSkill> JobSkillList(IEnumerable<string> names)
        => names.Select(n => new JobSkill { Name = n }).ToList();

    /// <summary>Deterministic Guid from a short string, so re-seeds produce the same IDs.</summary>
    private static Guid StableGuid(string key)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("jobnet:" + key));
        return new Guid(bytes);
    }
}
