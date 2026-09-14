namespace IMS.Domain.Entities;

public class Payment
{
    public long PaymentId { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public long ContractId { get; set; }
    public long ReceivedByUserId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public Contract Contract { get; set; } = null!;

    public User ReceivedByUser { get; set; } = null!;

    public ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
}
