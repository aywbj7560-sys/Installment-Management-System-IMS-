namespace IMS.Domain.Entities;

public class PaymentAllocation
{
    public long PaymentAllocationId { get; set; }
    public long PaymentId { get; set; }
    public long InstallmentId { get; set; }
    public decimal AllocatedAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    public Payment Payment { get; set; } = null!;

    public Installment Installment { get; set; } = null!;
}
