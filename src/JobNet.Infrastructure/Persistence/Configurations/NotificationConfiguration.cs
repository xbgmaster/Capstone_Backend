using JobNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobNet.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(n => n.Id);
        b.Property(n => n.Title).HasMaxLength(200).IsRequired();
        b.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        b.Property(n => n.Link).HasMaxLength(500);
        b.Property(n => n.Type).HasConversion<int>();

        b.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(n => new { n.UserId, n.CreatedAt });
    }
}
