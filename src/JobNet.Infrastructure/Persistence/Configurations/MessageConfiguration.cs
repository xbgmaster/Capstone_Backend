using JobNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobNet.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.ToTable("Messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.Body).HasMaxLength(4000).IsRequired();

        // Two FKs to Users -> use NoAction to avoid SQL Server multiple
        // cascade paths. Messages are cleaned up manually if a user is removed.
        b.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(m => m.Recipient)
            .WithMany()
            .HasForeignKey(m => m.RecipientId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(m => m.Job)
            .WithMany()
            .HasForeignKey(m => m.JobId)
            .OnDelete(DeleteBehavior.NoAction);

        // Fast lookup of a conversation between two users, optionally by job.
        b.HasIndex(m => new { m.SenderId, m.RecipientId, m.CreatedAt });
        b.HasIndex(m => new { m.RecipientId, m.Read });
    }
}
