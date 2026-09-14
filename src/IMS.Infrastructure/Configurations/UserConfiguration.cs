using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "public");
        builder.HasKey(x => x.UserId).HasName("users_pkey");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn()
            .IsRequired(true);

        builder.Property(x => x.RoleId)
            .HasColumnName("role_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.Username)
            .HasColumnName("username")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired(true);

        builder.HasIndex(x => x.Username).IsUnique().HasDatabaseName("users_username_key");

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired(true);

        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("users_email_key");

        builder.Property(x => x.PasswordHash)
            .HasColumnName("password_hash")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255)
            .IsRequired(true);

        builder.Property(x => x.FullName)
            .HasColumnName("full_name")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired(true);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired(true);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired(true);

        builder.HasOne(x => x.Role)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("users_role_id_fkey");

        builder.HasIndex(x => x.RoleId)
            .HasDatabaseName("idx_users_role_id");

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("idx_users_is_active");
    }
}
