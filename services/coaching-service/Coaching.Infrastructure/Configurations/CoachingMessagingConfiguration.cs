using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coaching.Infrastructure.Configurations;

/// <summary>Roadmap O3: teacher–student and teacher–parent conversations.</summary>
public sealed class CoachingConversationConfiguration : IEntityTypeConfiguration<CoachingConversation>
{
    public void Configure(EntityTypeBuilder<CoachingConversation> builder)
    {
        builder.ToTable("coaching_conversations", table =>
        {
            table.HasCheckConstraint("ck_coaching_conversation_kind",
                "(\"Kind\" = 'TeacherStudent' AND \"CounterpartUserId\" = \"StudentUserId\") OR (\"Kind\" = 'TeacherParent' AND \"CounterpartUserId\" <> \"StudentUserId\")");
            table.HasCheckConstraint("ck_coaching_conversation_participants", "\"TeacherUserId\" <> \"CounterpartUserId\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.TeacherUserId, x.CounterpartUserId, x.StudentUserId }).IsUnique();
        builder.HasIndex(x => new { x.TeacherUserId, x.LastMessageAt });
        builder.HasIndex(x => new { x.CounterpartUserId, x.LastMessageAt });
        builder.HasIndex(x => new { x.InstitutionId, x.LastMessageAt });
        builder.HasIndex(x => x.StudentUserId);
        builder.HasIndex(x => x.LastMessageAt);
    }
}

public sealed class CoachingMessageConfiguration : IEntityTypeConfiguration<CoachingMessage>
{
    public void Configure(EntityTypeBuilder<CoachingMessage> builder)
    {
        builder.ToTable("coaching_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Body).HasMaxLength(CoachingMessage.MaxBodyLength).IsRequired();
        builder.HasOne<CoachingConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ConversationId, x.SentAt });
    }
}

public sealed class CoachingMessageAttachmentConfiguration : IEntityTypeConfiguration<CoachingMessageAttachment>
{
    public void Configure(EntityTypeBuilder<CoachingMessageAttachment> builder)
    {
        builder.ToTable("coaching_message_attachments", table =>
            table.HasCheckConstraint("ck_coaching_message_attachment_size", "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 10485760"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(200).IsRequired();
        builder.HasOne<CoachingConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CoachingMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.ConversationId, x.MessageId });
        builder.HasIndex(x => new { x.MessageId, x.CreatedAt });
    }
}

public sealed class CoachingConversationAccessConfiguration : IEntityTypeConfiguration<CoachingConversationAccess>
{
    public void Configure(EntityTypeBuilder<CoachingConversationAccess> builder)
    {
        builder.ToTable("coaching_conversation_accesses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.HasOne<CoachingConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ConversationId, x.AccessedAt });
        builder.HasIndex(x => x.ViewerUserId);
    }
}
