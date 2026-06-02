using JobNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobNet.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("Reviews");
        b.HasKey(r => r.Id);
        b.Property(r => r.Comment).HasMaxLength(2000).IsRequired();

        b.HasOne(r => r.FromUser)
            .WithMany()
            .HasForeignKey(r => r.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(r => r.ToUser)
            .WithMany()
            .HasForeignKey(r => r.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(r => r.ToCompany)
            .WithMany()
            .HasForeignKey(r => r.ToCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(r => r.Job)
            .WithMany()
            .HasForeignKey(r => r.JobId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(r => r.ToCompanyId);
        b.HasIndex(r => r.ToUserId);
    }
}
