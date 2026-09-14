using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public class GuarantorConfiguration : IEntityTypeConfiguration<Guarantor>
{
    public void Configure(EntityTypeBuilder<Guarantor> builder)
    {
        builder.ToTable("guarantors", "public");
        builder.HasKey(x => x.GuarantorId).HasName("guarantors_pkey");

        builder.Property(x => x.GuarantorId)
            .HasColumnName("guarantor_id")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn()
            .IsRequired(true);

        builder.Property(x => x.FullName)
            .HasColumnName("full_name")
            .HasColumnType("varchar(150)")
            .HasMaxLength(150)
            .IsRequired(true);

        builder.Property(x => x.IdentificationNumber)
            .HasColumnName("identification_number")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired(true);

        builder.HasIndex(x => x.IdentificationNumber).IsUnique().HasDatabaseName("guarantors_identification_number_key");

        builder.Property(x => x.Phone)
            .HasColumnName("phone")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired(true);

        builder.Property(x => x.SecondaryPhone)
            .HasColumnName("secondary_phone")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(x => x.Address)
            .HasColumnName("address")
            .HasColumnType("text")
            .IsRequired(true);

        builder.Property(x => x.Occupation)
            .HasColumnName("occupation")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(x => x.Workplace)
            .HasColumnName("workplace")
            .HasColumnType("varchar(150)")
            .HasMaxLength(150)
            .IsRequired(false);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasColumnType("text")
            .IsRequired(false);

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

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("idx_guarantors_is_active");

        builder.HasIndex(x => x.Phone)
            .HasDatabaseName("idx_guarantors_phone");
    }
}
