using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public class ContractItemConfiguration : IEntityTypeConfiguration<ContractItem>
{
    public void Configure(EntityTypeBuilder<ContractItem> builder)
    {
        builder.ToTable("contract_items", "public", table =>
        {
            table.HasCheckConstraint("contract_items_quantity_check", "quantity > 0");
            table.HasCheckConstraint("contract_items_unit_price_check", "unit_price >= 0.00");
            table.HasCheckConstraint("contract_items_subtotal_check", "subtotal >= 0.00");
        });
        builder.HasKey(x => x.ContractItemId).HasName("contract_items_pkey");

        builder.Property(x => x.ContractItemId)
            .HasColumnName("contract_item_id")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn()
            .IsRequired(true);

        builder.Property(x => x.ContractId)
            .HasColumnName("contract_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("integer")
            .IsRequired(true);

        builder.Property(x => x.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .IsRequired(true);

        builder.Property(x => x.Subtotal)
            .HasColumnName("subtotal")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .IsRequired(true);

        builder.HasOne(x => x.Contract)
            .WithMany(x => x.ContractItems)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("contract_items_contract_id_fkey");

        builder.HasOne(x => x.Product)
            .WithMany(x => x.ContractItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("contract_items_product_id_fkey");

        builder.HasIndex(x => x.ProductId)
            .HasDatabaseName("idx_contract_items_product_id");

        builder.HasIndex(x => new { x.ContractId, x.ProductId }).IsUnique()
            .HasDatabaseName("uq_contract_items_contract_product");
    }
}
