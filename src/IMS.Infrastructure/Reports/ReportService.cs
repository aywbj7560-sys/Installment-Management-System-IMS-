using System.Data;
using System.Linq.Expressions;
using IMS.Application.Installments;
using IMS.Application.Reports;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IMS.Infrastructure.Reports;

public sealed class ReportService(ImsDbContext db, TimeProvider clock) : IReportService
{
    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    // Consistent multi-query snapshots; PostgreSQL also enforces that reporting cannot write.
    private async Task<T> Snapshot<T>(Func<Task<T>> read, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return await read(); // isolated in-memory tests
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        var result = await read();
        await transaction.CommitAsync(ct);
        return result;
    }

    public Task<ReportSummary> SummaryAsync(CancellationToken ct) => Snapshot(async () =>
    {
        var today = Today();
        var contracts = db.Contracts.AsNoTracking();
        var installments = db.Installments.AsNoTracking();
        var open = Open(installments);
        var past = installments.Where(x => x.DueDate < today && x.RemainingAmount > 0);
        var groups = await contracts.GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.LongCount() }).ToListAsync(ct);
        var statuses = Enum.GetValues<ContractStatus>().ToDictionary(x => x.ToString(), x => groups.SingleOrDefault(g => g.Status == x)?.Count ?? 0);
        return new ReportSummary(await db.Customers.AsNoTracking().LongCountAsync(ct),
            await db.Customers.AsNoTracking().LongCountAsync(x => x.Status == CustomerStatus.Active, ct),
            await db.Products.AsNoTracking().LongCountAsync(ct), await db.Products.AsNoTracking().LongCountAsync(x => x.IsActive, ct),
            statuses.Values.Sum(), statuses, statuses[nameof(ContractStatus.Completed)],
            await contracts.SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0,
            await contracts.SumAsync(x => (decimal?)(x.TotalAmount - x.DownPayment), ct) ?? 0,
            await contracts.SumAsync(x => (decimal?)x.RemainingAmount, ct) ?? 0,
            await contracts.Where(x => x.Status == ContractStatus.Active).SumAsync(x => (decimal?)x.RemainingAmount, ct) ?? 0,
            await db.Payments.AsNoTracking().SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
            await open.LongCountAsync(ct), await open.SumAsync(x => (decimal?)x.RemainingAmount, ct) ?? 0,
            await past.LongCountAsync(ct), await past.SumAsync(x => (decimal?)x.RemainingAmount, ct) ?? 0, today);
    }, ct);

    public Task<ReportPage<ContractReportRow>> ContractsAsync(ReportQuery filter, CancellationToken ct) => Snapshot(async () =>
    {
        var query = db.Contracts.AsNoTracking();
        if (filter.CustomerId.HasValue) query = query.Where(x => x.CustomerId == filter.CustomerId);
        if (filter.ContractId.HasValue) query = query.Where(x => x.ContractId == filter.ContractId);
        if (filter.GetStatus() is { } status) query = query.Where(x => x.Status == status);
        if (filter.From() is { } from) { var start = Utc(from); query = query.Where(x => x.ContractDate >= start); }
        if (filter.To() is { } to) { var end = Utc(to.AddDays(1)); query = query.Where(x => x.ContractDate < end); }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        { var term = filter.Search.Trim().ToLowerInvariant(); query = query.Where(x => x.ContractNumber.ToLower().Contains(term) || x.Customer.FullName.ToLower().Contains(term)); }
        return await Page(query.OrderBy(x => x.ContractDate).ThenBy(x => x.ContractId).Select(ContractView), filter.Page, filter.PageSize, Today(), ct);
    }, ct);

    public Task<ReportPage<PaymentReportRow>> PaymentsAsync(ReportQuery filter, CancellationToken ct) => Snapshot(async () =>
    {
        var query = db.Payments.AsNoTracking();
        if (filter.CustomerId.HasValue) query = query.Where(x => x.Contract.CustomerId == filter.CustomerId);
        if (filter.ContractId.HasValue) query = query.Where(x => x.ContractId == filter.ContractId);
        if (filter.From() is { } from) { var start = Utc(from); query = query.Where(x => x.PaymentDate >= start); }
        if (filter.To() is { } to) { var end = Utc(to.AddDays(1)); query = query.Where(x => x.PaymentDate < end); }
        if (filter.PaymentMethod is not null) query = query.Where(x => x.PaymentMethod == filter.PaymentMethod.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Search))
        { var term = filter.Search.Trim().ToLowerInvariant(); query = query.Where(x => x.PaymentReference.ToLower().Contains(term) || x.Contract.ContractNumber.ToLower().Contains(term) || x.Contract.Customer.FullName.ToLower().Contains(term)); }
        return await PaymentPage(query, filter.Page, filter.PageSize, Today(), ct);
    }, ct);

    public Task<ReportPage<InstallmentView>> OutstandingAsync(InstallmentQuery filter, CancellationToken ct) => Snapshot(async () =>
    {
        var today = Today();
        var query = Open(db.Installments.AsNoTracking()).Where(x => x.Contract.Status == ContractStatus.Active);
        if (filter.CustomerId.HasValue) query = query.Where(x => x.Contract.CustomerId == filter.CustomerId);
        if (filter.ContractId.HasValue) query = query.Where(x => x.ContractId == filter.ContractId);
        if (filter.GetStatus() is { } status) query = query.Where(x => x.Status == status);
        if (filter.GetFrom() is { } from) query = query.Where(x => x.DueDate >= from);
        if (filter.GetTo() is { } to) query = query.Where(x => x.DueDate <= to);
        if (filter.PastDueOnly) query = query.Where(x => x.DueDate < today && x.RemainingAmount > 0);
        if (filter.InstallmentNumber.HasValue) query = query.Where(x => x.InstallmentNumber == filter.InstallmentNumber);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        { var term = filter.Search.Trim().ToLowerInvariant(); query = query.Where(x => x.Contract.ContractNumber.ToLower().Contains(term) || x.Contract.Customer.FullName.ToLower().Contains(term)); }
        return await InstallmentPage(query, filter.Page, filter.PageSize, today, ct);
    }, ct);

    public Task<CustomerStatement?> CustomerStatementAsync(long id, StatementQuery filter, CancellationToken ct) => Snapshot<CustomerStatement?>(async () =>
    {
        var today = Today();
        var customer = await db.Customers.AsNoTracking().Where(x => x.CustomerId == id)
            .Select(x => new ReportCustomer(x.CustomerId, x.FullName, x.Phone, x.Status.ToString())).SingleOrDefaultAsync(ct);
        if (customer is null) return null;
        var contracts = db.Contracts.AsNoTracking().Where(x => x.CustomerId == id);
        var installments = db.Installments.AsNoTracking().Where(x => x.Contract.CustomerId == id);
        var payments = db.Payments.AsNoTracking().Where(x => x.Contract.CustomerId == id);
        return new(customer, await Page(contracts.OrderBy(x => x.ContractId).Select(ContractView), filter.ContractsPage, filter.PageSize, today, ct),
            await InstallmentPage(installments, filter.InstallmentsPage, filter.PageSize, today, ct),
            await PaymentPage(payments, filter.PaymentsPage, filter.PageSize, today, ct),
            await Totals(contracts, installments, payments, db.PaymentAllocations.AsNoTracking().Where(x => x.Payment.Contract.CustomerId == id), ct), today);
    }, ct);

    public Task<ContractStatement?> ContractStatementAsync(long id, StatementQuery filter, CancellationToken ct) => Snapshot<ContractStatement?>(async () =>
    {
        var today = Today();
        var contracts = db.Contracts.AsNoTracking().Where(x => x.ContractId == id);
        var contract = await contracts.Select(ContractView).SingleOrDefaultAsync(ct);
        if (contract is null) return null;
        var installments = db.Installments.AsNoTracking().Where(x => x.ContractId == id);
        var payments = db.Payments.AsNoTracking().Where(x => x.ContractId == id);
        var items = db.ContractItems.AsNoTracking().Where(x => x.ContractId == id).OrderBy(x => x.ContractItemId)
            .Select(x => new StatementItem(x.ContractItemId, x.ProductId, x.Product.ProductCode, x.Product.Name, x.Quantity, x.UnitPrice, x.Subtotal));
        var guarantors = db.ContractGuarantors.AsNoTracking().Where(x => x.ContractId == id).OrderBy(x => x.ContractGuarantorId)
            .Select(x => new StatementGuarantor(x.ContractGuarantorId, x.GuarantorId, x.Guarantor.FullName, x.Guarantor.Phone, x.Guarantor.IsActive, x.Notes));
        return new(contract, await Page(items, filter.ItemsPage, filter.PageSize, today, ct), await Page(guarantors, filter.GuarantorsPage, filter.PageSize, today, ct),
            await installments.OrderBy(x => x.InstallmentNumber).Select(InstallmentView(today)).ToListAsync(ct),
            await PaymentPage(payments, filter.PaymentsPage, filter.PageSize, today, ct),
            await Totals(contracts, installments, payments, db.PaymentAllocations.AsNoTracking().Where(x => x.Payment.ContractId == id), ct), today);
    }, ct);

    private static DateTime Utc(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    private static IQueryable<Installment> Open(IQueryable<Installment> query) => query.Where(x => x.RemainingAmount > 0 && x.Status != InstallmentStatus.Paid && x.Status != InstallmentStatus.Waived);
    private static async Task<ReportPage<T>> Page<T>(IQueryable<T> query, int page, int size, DateOnly today, CancellationToken ct) =>
        new(await query.Skip((page - 1) * size).Take(size).ToListAsync(ct), await query.CountAsync(ct), page, size, today);
    private Task<ReportPage<PaymentReportRow>> PaymentPage(IQueryable<Payment> query, int page, int size, DateOnly today, CancellationToken ct) =>
        Page(query.OrderBy(x => x.PaymentDate).ThenBy(x => x.PaymentId).Select(PaymentView), page, size, today, ct);
    private Task<ReportPage<InstallmentView>> InstallmentPage(IQueryable<Installment> query, int page, int size, DateOnly today, CancellationToken ct) =>
        Page(query.OrderBy(x => x.DueDate).ThenBy(x => x.InstallmentNumber).ThenBy(x => x.ContractId).ThenBy(x => x.InstallmentId).Select(InstallmentView(today)), page, size, today, ct);
    private static async Task<StatementTotals> Totals(IQueryable<Contract> contracts, IQueryable<Installment> installments,
        IQueryable<Payment> payments, IQueryable<PaymentAllocation> allocations, CancellationToken ct) => new(
        await contracts.SumAsync(x => (decimal?)x.RemainingAmount, ct) ?? 0,
        await installments.SumAsync(x => (decimal?)x.RemainingAmount, ct) ?? 0,
        await installments.SumAsync(x => (decimal?)x.PaidAmount, ct) ?? 0,
        await payments.SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
        await allocations.SumAsync(x => (decimal?)x.AllocatedAmount, ct) ?? 0);

    private static readonly Expression<Func<Contract, ContractReportRow>> ContractView = x => new(x.ContractId, x.ContractNumber,
        new(x.CustomerId, x.Customer.FullName, x.Customer.Phone, x.Customer.Status.ToString()), x.ContractDate,
        x.TotalAmount, x.DownPayment, x.RemainingAmount, x.Status.ToString(), x.NumberOfInstallments,
        x.Installments.Count(i => i.Status == InstallmentStatus.Paid),
        x.Installments.Count(i => i.RemainingAmount > 0 && i.Status != InstallmentStatus.Paid && i.Status != InstallmentStatus.Waived));
    private static readonly Expression<Func<Payment, PaymentReportRow>> PaymentView = x => new(x.PaymentId, x.PaymentReference,
        x.PaymentDate, x.Amount, x.PaymentMethod, x.ContractId, x.Contract.ContractNumber,
        new(x.Contract.CustomerId, x.Contract.Customer.FullName, x.Contract.Customer.Phone, x.Contract.Customer.Status.ToString()),
        x.ReceivedByUserId, x.ReceivedByUser.FullName, x.PaymentAllocations.Sum(a => (decimal?)a.AllocatedAmount) ?? 0);
    private static Expression<Func<Installment, InstallmentView>> InstallmentView(DateOnly today) => x => new(
        x.InstallmentId, x.InstallmentNumber, x.DueDate, x.Amount, x.PaidAmount, x.RemainingAmount,
        x.Status == InstallmentStatus.PartiallyPaid ? "Partially Paid" : x.Status.ToString(),
        x.DueDate < today && x.RemainingAmount > 0,
        x.RemainingAmount > 0 && x.Status != InstallmentStatus.Paid && x.Status != InstallmentStatus.Waived,
        new(x.ContractId, x.Contract.ContractNumber, x.Contract.Status.ToString()),
        new(x.Contract.CustomerId, x.Contract.Customer.FullName, x.Contract.Customer.Phone, x.Contract.Customer.SecondaryPhone));
}
