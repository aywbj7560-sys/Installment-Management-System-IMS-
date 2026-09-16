using System.Globalization;
using IMS.Domain.Enums;

namespace IMS.Application.Installments;

public sealed record InstallmentQuery(long? ContractId = null, long? CustomerId = null, string? Status = null,
    string? DueFrom = null, string? DueTo = null, bool PastDueOnly = false, bool OpenOnly = false,
    int? InstallmentNumber = null, string? Search = null, int Page = 1, int PageSize = 50)
{
    public Dictionary<string, string[]> Validate(bool collectionQueue = false)
    {
        var errors = new Dictionary<string, string[]>();
        if (ContractId <= 0 || CustomerId <= 0) errors["ids"] = ["IDs must be positive."];
        if (Page < 1 || PageSize < 1 || PageSize > 100 || Page > int.MaxValue / PageSize)
            errors["pagination"] = ["Use a positive page and pageSize 1-100 within the supported offset range."];
        if (InstallmentNumber is < 1 or > 12) errors["installmentNumber"] = ["Use installment number 1-12."];
        if (Search?.Length > 150 || Search?.Contains('\0') == true) errors["search"] = ["Use at most 150 characters without null characters."];
        if (Status is not null && ParsedStatus is null) errors["status"] = ["Use Pending, Paid, Partially Paid (or PartiallyPaid), Overdue, or Waived."];
        if (DueFrom is not null && From is null || DueTo is not null && To is null)
            errors["dates"] = ["Use finite dates in yyyy-MM-dd format."];
        if (From > To) errors["dates"] = ["dueFrom must be on or before dueTo."];
        if ((OpenOnly || collectionQueue) && ParsedStatus is InstallmentStatus.Paid or InstallmentStatus.Waived ||
            PastDueOnly && ParsedStatus == InstallmentStatus.Paid)
            errors["filters"] = ["The selected status is incompatible with the requested open/past-due view."];
        return errors;
    }

    // Methods keep parsed helpers out of ASP.NET's AsParameters query binding.
    public InstallmentStatus? GetStatus() => ParsedStatus;
    public DateOnly? GetFrom() => From;
    public DateOnly? GetTo() => To;
    private InstallmentStatus? ParsedStatus => Status switch
    {
        "Pending" => InstallmentStatus.Pending, "Paid" => InstallmentStatus.Paid,
        "Partially Paid" or "PartiallyPaid" => InstallmentStatus.PartiallyPaid,
        "Overdue" => InstallmentStatus.Overdue, "Waived" => InstallmentStatus.Waived, _ => null
    };
    private DateOnly? From => ParseDate(DueFrom);
    private DateOnly? To => ParseDate(DueTo);
    private static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        && date != DateOnly.MinValue && date != DateOnly.MaxValue ? date : null;
}

public sealed record InstallmentContractSummary(long ContractId, string ContractNumber, string Status);
public sealed record InstallmentCustomerSummary(long CustomerId, string FullName, string Phone, string? SecondaryPhone);
public sealed record InstallmentView(long InstallmentId, int InstallmentNumber, DateOnly DueDate,
    decimal Amount, decimal PaidAmount, decimal RemainingAmount, string Status, bool IsPastDue,
    bool IsOpen, InstallmentContractSummary Contract, InstallmentCustomerSummary Customer);
public sealed record InstallmentPage(IReadOnlyList<InstallmentView> Items, int TotalCount, int Page, int PageSize, DateOnly AsOfUtcDate);
public sealed record InstallmentDetails(InstallmentView Installment, DateOnly AsOfUtcDate);
public sealed record ContractSchedule(long ContractId, IReadOnlyList<InstallmentView> Items, DateOnly AsOfUtcDate);

public interface IInstallmentService
{
    Task<InstallmentPage> ListAsync(InstallmentQuery query, bool collectionQueue, CancellationToken ct);
    Task<InstallmentDetails?> GetAsync(long id, CancellationToken ct);
    Task<ContractSchedule?> ScheduleAsync(long contractId, CancellationToken ct);
}
