using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", "public");
        builder.HasKey(x => x.AuditLogId).HasName("audit_logs_pkey");
        builder.Property(x => x.AuditLogId).HasColumnName("audit_log_id").HasColumnType("bigint").UseIdentityByDefaultColumn();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("bigint").IsRequired();
        builder.Property(x => x.ActionType).HasColumnName("action_type").HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
        builder.Property(x => x.TargetEntityType).HasColumnName("target_entity_type").HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
        builder.Property(x => x.TargetEntityId).HasColumnName("target_entity_id").HasColumnType("bigint").IsRequired();
        builder.Property(x => x.Timestamp).HasColumnName("timestamp").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.PreviousState).HasColumnName("previous_state").HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
        builder.Property(x => x.NewState).HasColumnName("new_state").HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Reference).HasColumnName("reference").HasColumnType("varchar(100)").HasMaxLength(100);
        builder.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        builder.HasOne(x => x.User).WithMany(x => x.AuditLogs).HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("audit_logs_user_id_fkey");
        builder.HasIndex(x => x.UserId).HasDatabaseName("idx_audit_logs_user_id");
        builder.HasIndex(x => new { x.ActionType, x.TargetEntityType, x.TargetEntityId }).IsUnique()
            .HasDatabaseName("uq_audit_logs_action_target");
    }
}
