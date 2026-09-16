using System.Globalization;
using IMS.Application.Installments;
using IMS.Domain.Enums;

namespace IMS.Application.Reports;

public sealed record ReportQuery(long? CustomerId = null, long? ContractId = null, string? Status = null,
    string? DateFrom = null, string? DateTo = null, string? PaymentMethod = null, string? Search = null,
    int Page = 1, int PageSize = 50)
{
    public Dictionary<string, string[]> Validate(bool payments)
    {
        var errors = ReportValidation.Page(Page, PageSize);
        if (CustomerId <= 0 || ContractId <= 0) errors["ids"] = ["IDs must be positive."];
        if (Search?.Length > 150 || Search?.Contains('\0') == true) errors["search"] = ["Search permits at most 150 characters without null characters."];
        if (payments && Status is not null || !payments && PaymentMethod is not null) errors["filters"] = ["Filter is not supported by this report."];
        if (!payments && Status is not null && GetStatus() is null) errors["status"] = ["Invalid contract status."];
        if (PaymentMethod is not null && (string.IsNullOrWhiteSpace(PaymentMethod) || PaymentMethod.Length > 30 || PaymentMethod.Contains('\0')))
            errors["paymentMethod"] = ["Use a nonblank payment method up to 30 characters."];
        if (DateFrom is not null && From() is null || DateTo is not null && To() is null || From() > To())
            errors["dates"] = ["Use an ordered inclusive UTC date range in yyyy-MM-dd format."];
        return errors;
    }
    public ContractStatus? GetStatus() => Enum.TryParse<ContractStatus>(Status, out var value) && Enum.IsDefined(value) && value.ToString() == Status ? value : null;
    public DateOnly? From() => ReportValidation.Date(DateFrom);
    public DateOnly? To() => ReportValidation.Date(DateTo);
}

public sealed record StatementQuery(int ContractsPage = 1, int InstallmentsPage = 1, int PaymentsPage = 1,
    int ItemsPage = 1, int GuarantorsPage = 1, int PageSize = 25)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var page in new[] { ContractsPage, InstallmentsPage, PaymentsPage, ItemsPage, GuarantorsPage })
            foreach (var error in ReportValidation.Page(page, PageSize)) errors[error.Key] = error.Value;
        return errors;
    }
}

public static class ReportValidation
{
    public static Dictionary<string, string[]> Page(int page, int size) =>
        page < 1 || size < 1 || size > 100 || page > int.MaxValue / size
        ? new() { ["pagination"] = ["Use positive pages and pageSize 1-100 within supported offsets."] } : new();
    public static DateOnly? Date(string? value) => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var date) && date != DateOnly.MinValue && date != DateOnly.MaxValue ? date : null;
}

public sealed record ReportPage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize, DateOnly AsOfUtcDate);
public sealed record ReportCustomer(long CustomerId, string FullName, string Phone, string Status);
public sealed record ContractReportRow(long ContractId, string ContractNumber, ReportCustomer Customer, DateTime ContractDate,
    decimal TotalAmount, decimal DownPayment, decimal RemainingAmount, string Status, int NumberOfInstallments, int PaidInstallmentCount, int OpenInstallmentCount);
public sealed record PaymentReportRow(long PaymentId, string PaymentReference, DateTime PaymentDate, decimal Amount,
    string PaymentMethod, long ContractId, string ContractNumber, ReportCustomer Customer, long ReceivedByUserId, string ReceivedByName, decimal AllocatedTotal);
public sealed record ReportSummary(long TotalCustomers, long ActiveCustomers, long TotalProducts, long ActiveProducts,
    long TotalContracts, IReadOnlyDictionary<string, long> ContractsByStatus, long CompletedContracts,
    decimal TotalContractValue, decimal TotalFinancedPrincipal, decimal TotalContractRemaining, decimal ActiveContractRemaining,
    decimal TotalPaymentsReceived, long OpenInstallmentCount, decimal OpenInstallmentBalance,
    long PastDueInstallmentCount, decimal PastDueInstallmentBalance, DateOnly AsOfUtcDate);
public sealed record StatementTotals(decimal ContractRemaining, decimal InstallmentRemaining, decimal InstallmentPaid,
    decimal PaymentsReceived, decimal AllocationsTotal);
public sealed record CustomerStatement(ReportCustomer Customer, ReportPage<ContractReportRow> Contracts,
    ReportPage<InstallmentView> Installments, ReportPage<PaymentReportRow> Payments, StatementTotals Totals, DateOnly AsOfUtcDate);
public sealed record StatementItem(long ContractItemId, long ProductId, string ProductCode, string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal);
public sealed record StatementGuarantor(long ContractGuarantorId, long GuarantorId, string FullName, string Phone, bool IsActive, string? GuaranteeNotes);
public sealed record ContractStatement(ContractReportRow Contract, ReportPage<StatementItem> Items, ReportPage<StatementGuarantor> Guarantors,
    IReadOnlyList<InstallmentView> Installments, ReportPage<PaymentReportRow> Payments, StatementTotals Totals, DateOnly AsOfUtcDate);

public interface IReportService
{
    Task<ReportSummary> SummaryAsync(CancellationToken ct);
    Task<ReportPage<ContractReportRow>> ContractsAsync(ReportQuery query, CancellationToken ct);
    Task<ReportPage<PaymentReportRow>> PaymentsAsync(ReportQuery query, CancellationToken ct);
    Task<ReportPage<InstallmentView>> OutstandingAsync(InstallmentQuery query, CancellationToken ct);
    Task<CustomerStatement?> CustomerStatementAsync(long id, StatementQuery query, CancellationToken ct);
    Task<ContractStatement?> ContractStatementAsync(long id, StatementQuery query, CancellationToken ct);
}
