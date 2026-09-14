namespace IMS.Domain.Entities;

public class ContractItem
{
    public long ContractItemId { get; set; }
    public long ContractId { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    public Contract Contract { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
