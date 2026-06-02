using JobNet.Domain.Entities;
using JobNet.Infrastructure.Contracts;

namespace JobNet.Infrastructure.Mapping;

public static class Mapper
{
    public static UserDto ToDto(this User u) => new(
        u.Id, u.FirstName, u.LastName, u.Email, u.Role, u.Status,
        u.Phone, u.City, u.Province, u.Avatar, u.CompanyId, u.CreatedAt);

    public static CompanyDto ToDto(this Company c) => new(
        c.Id, c.OwnerId, c.Name, c.Industry, c.BusinessNumber, c.Website, c.Email, c.Phone,
        c.Address, c.City, c.Province, c.FoundedYear, c.EmployeeCount, c.Description,
        c.Rating, c.ReviewCount, c.Verified, c.CreatedAt);

    public static WorkerProfileDto ToDto(this WorkerProfile p) => new(
        p.UserId, p.Headline, p.Bio, p.YearsExperience, p.HourlyRate, p.Availability,
        p.Rating, p.ReviewCount,
        p.Skills.Select(s => s.Name).ToList(),
        p.Certifications.Select(c => new CertificationDto(c.Name, c.Issuer, c.Year)).ToList(),
        p.Experiences.Select(e => new ExperienceDto(e.Title, e.Company, e.From, e.To)).ToList());

    public static JobDto ToDto(this Job j) => new(
        j.Id, j.CompanyId, j.Company?.Name, j.Title, j.Category, j.Description, j.Activity,
        j.Location, j.DueDate, j.PaymentType, j.PaymentAmount, j.Currency, j.Status, j.PostedAt,
        j.SkillsRequired.Select(s => s.Name).ToList(),
        j.Applications?.Count ?? 0);

    public static ApplicationDto ToDto(this Application a) => new(
        a.Id,
        a.JobId, a.Job?.Title ?? string.Empty,
        a.Job?.CompanyId, a.Job?.Company?.Name,
        a.WorkerId,
        a.Worker?.FirstName ?? string.Empty,
        a.Worker?.LastName ?? string.Empty,
        a.Worker?.WorkerProfile?.Headline,
        a.Worker?.WorkerProfile?.Rating ?? 0,
        a.CoverLetter, a.ExpectedRate, a.Status, a.SubmittedAt);

    public static ReviewDto ToDto(this Review r) => new(
        r.Id, r.FromUserId,
        r.FromUser is null ? string.Empty : $"{r.FromUser.FirstName} {r.FromUser.LastName}".Trim(),
        r.ToUserId, r.ToCompanyId, r.JobId, r.Job?.Title,
        r.Rating, r.Comment, r.CreatedAt);

    public static NotificationDto ToDto(this Notification n) => new(
        n.Id, n.UserId, n.Type, n.Title, n.Message, n.Link, n.Read, n.CreatedAt);
}
