namespace IMS.Domain.Entities;

public class Product
{
    public long ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal CashPrice { get; set; }
    public decimal? InstallmentPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<ContractItem> ContractItems { get; set; } = new List<ContractItem>();
}
