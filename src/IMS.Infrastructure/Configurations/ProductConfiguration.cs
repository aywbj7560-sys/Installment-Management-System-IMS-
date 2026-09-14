using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "public", table =>
        {
            table.HasCheckConstraint("products_cash_price_check", "cash_price >= 0.00");
            table.HasCheckConstraint("products_installment_price_check", "installment_price IS NULL OR installment_price >= 0.00");
        });
        builder.HasKey(x => x.ProductId).HasName("products_pkey");

        builder.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn()
            .IsRequired(true);

        builder.Property(x => x.ProductCode)
            .HasColumnName("product_code")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired(true);

        builder.HasIndex(x => x.ProductCode).IsUnique().HasDatabaseName("products_product_code_key");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(150)")
            .HasMaxLength(150)
            .IsRequired(true);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(x => x.CashPrice)
            .HasColumnName("cash_price")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .IsRequired(true);

        builder.Property(x => x.InstallmentPrice)
            .HasColumnName("installment_price")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
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
            .HasDatabaseName("idx_products_is_active");
    }
}
