using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers", "public", table =>
        {
            table.HasCheckConstraint("customers_status_check", "status IN ('Active', 'Inactive', 'Blacklisted')");
        });
        builder.HasKey(x => x.CustomerId).HasName("customers_pkey");

        builder.Property(x => x.CustomerId)
            .HasColumnName("customer_id")
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

        builder.HasIndex(x => x.IdentificationNumber).IsUnique().HasDatabaseName("customers_identification_number_key");

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

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(x => x.Address)
            .HasColumnName("address")
            .HasColumnType("text")
            .IsRequired(true);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(CustomerStatus.Active)
            .IsRequired(true);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired(true);

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("idx_customers_status");

        builder.HasIndex(x => x.Phone)
            .HasDatabaseName("idx_customers_phone");
    }
}
