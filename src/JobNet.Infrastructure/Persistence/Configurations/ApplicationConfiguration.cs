using JobNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobNet.Infrastructure.Persistence.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> b)
    {
        b.ToTable("Applications");
        b.HasKey(a => a.Id);
        b.Property(a => a.CoverLetter).HasMaxLength(4000).IsRequired();
        b.Property(a => a.ExpectedRate).HasPrecision(12, 2);
        b.Property(a => a.Status).HasConversion<int>();

        b.HasOne(a => a.Worker)
            .WithMany()
            .HasForeignKey(a => a.WorkerId)
            .OnDelete(DeleteBehavior.Restrict);

        // One application per worker per job.
        b.HasIndex(a => new { a.JobId, a.WorkerId }).IsUnique();
    }
}
