using System.Linq.Expressions;
using IMS.Application.Installments;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IMS.Infrastructure.Installments;

public sealed class InstallmentService(ImsDbContext db, TimeProvider clock) : IInstallmentService
{
    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public async Task<InstallmentPage> ListAsync(InstallmentQuery filter, bool collectionQueue, CancellationToken ct)
    {
        var today = Today();
        var query = db.Installments.AsNoTracking();
        if (filter.ContractId.HasValue) query = query.Where(x => x.ContractId == filter.ContractId.Value);
        if (filter.CustomerId.HasValue) query = query.Where(x => x.Contract.CustomerId == filter.CustomerId.Value);
        if (filter.GetStatus() is { } status) query = query.Where(x => x.Status == status);
        if (filter.GetFrom() is { } from) query = query.Where(x => x.DueDate >= from);
        if (filter.GetTo() is { } to) query = query.Where(x => x.DueDate <= to);
        if (filter.InstallmentNumber.HasValue) query = query.Where(x => x.InstallmentNumber == filter.InstallmentNumber.Value);
        if (filter.PastDueOnly) query = query.Where(x => x.DueDate < today && x.RemainingAmount > 0);
        if (filter.OpenOnly || collectionQueue)
            query = query.Where(x => x.RemainingAmount > 0 && x.Status != InstallmentStatus.Paid && x.Status != InstallmentStatus.Waived);
        if (collectionQueue) query = query.Where(x => x.Contract.Status == ContractStatus.Active && x.DueDate <= today);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Contract.ContractNumber.ToLower().Contains(term) || x.Contract.Customer.FullName.ToLower().Contains(term));
        }
        var count = await query.CountAsync(ct);
        // In the due queue, chronological ordering puts all past-due rows before today's rows.
        var rows = await query.OrderBy(x => x.DueDate).ThenBy(x => x.InstallmentNumber)
            .ThenBy(x => x.ContractId).ThenBy(x => x.InstallmentId)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(View(today)).ToListAsync(ct);
        return new(rows, count, filter.Page, filter.PageSize, today);
    }

    public async Task<InstallmentDetails?> GetAsync(long id, CancellationToken ct)
    {
        var today = Today();
        var row = await db.Installments.AsNoTracking().Where(x => x.InstallmentId == id).Select(View(today)).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row, today);
    }

    public async Task<ContractSchedule?> ScheduleAsync(long contractId, CancellationToken ct)
    {
        var today = Today();
        if (!await db.Contracts.AsNoTracking().AnyAsync(x => x.ContractId == contractId, ct)) return null;
        var rows = await db.Installments.AsNoTracking().Where(x => x.ContractId == contractId)
            .OrderBy(x => x.InstallmentNumber).ThenBy(x => x.DueDate).Select(View(today)).ToListAsync(ct);
        return new(contractId, rows, today);
    }

    private static Expression<Func<Installment, InstallmentView>> View(DateOnly today) => x => new(
        x.InstallmentId, x.InstallmentNumber, x.DueDate, x.Amount, x.PaidAmount, x.RemainingAmount,
        x.Status == InstallmentStatus.PartiallyPaid ? "Partially Paid" : x.Status.ToString(),
        x.DueDate < today && x.RemainingAmount > 0,
        x.RemainingAmount > 0 && x.Status != InstallmentStatus.Paid && x.Status != InstallmentStatus.Waived,
        new(x.ContractId, x.Contract.ContractNumber, x.Contract.Status.ToString()),
        new(x.Contract.CustomerId, x.Contract.Customer.FullName, x.Contract.Customer.Phone, x.Contract.Customer.SecondaryPhone));
}
