using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using IMS.Application.Contracts;

namespace IMS.Application.Payments;

// Allocation targets are server-owned (FR-012). Reject unknown fields so client
// installment IDs/allocations cannot appear to have been accepted and then ignored.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PaymentRequest
{
    [Range(1, long.MaxValue)] public long ContractId { get; init; }
    [Required, StringLength(50)] public string PaymentReference { get; init; } = "";
    public DateTimeOffset? PaymentDate { get; init; }
    public decimal Amount { get; init; }
    [Required, StringLength(30)] public string PaymentMethod { get; init; } = "";
    public string? Notes { get; init; }

    public Dictionary<string, string[]> Validate()
    {
        var errors = ContractValidation.Fields(this);
        if (Amount <= 0 || !ContractValidation.Money(Amount))
            errors["Amount"] = ["Use a positive numeric(15,2) amount with no fractional cents."];
        if (PaymentDate.HasValue && (PaymentDate.Value.UtcDateTime == DateTime.MinValue || PaymentDate.Value.UtcDateTime == DateTime.MaxValue))
            errors["PaymentDate"] = ["Provide a finite payment timestamp."];
        return errors;
    }
}

public sealed record PaymentSummary(long PaymentId, string PaymentReference, long ContractId,
    long ReceivedByUserId, DateTime PaymentDate, decimal Amount, string PaymentMethod, string? Notes);
public sealed record PaymentContractSummary(long ContractId, string ContractNumber, long CustomerId,
    string CustomerName, decimal RemainingAmount, string Status);
public sealed record PaymentAllocationResponse(long PaymentAllocationId, decimal AllocatedAmount,
    DateTime CreatedAt, InstallmentResponse Installment);
public sealed record PaymentDetails(PaymentSummary Payment, PaymentContractSummary Contract,
    IReadOnlyList<PaymentAllocationResponse> Allocations);
public sealed record PaymentPage(IReadOnlyList<PaymentSummary> Items, int TotalCount, int Page, int PageSize);
public sealed record PaymentResult(PaymentDetails? Payment, int ErrorStatus = 0, string? Message = null);

public interface IPaymentService
{
    Task<PaymentResult> CreateAsync(PaymentRequest request, long userId, CancellationToken ct);
    Task<PaymentDetails?> GetAsync(long id, CancellationToken ct);
    Task<PaymentPage> ListAsync(string? search, long? contractId, long? customerId, int page, int pageSize, CancellationToken ct);
    Task<bool> ContractExistsAsync(long contractId, CancellationToken ct);
}
