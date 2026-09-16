using System.Data;
using IMS.Application.Contracts;
using IMS.Application.Payments;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IMS.Infrastructure.Payments;

public sealed class PaymentService(ImsDbContext db) : IPaymentService
{
    public async Task<PaymentResult> CreateAsync(PaymentRequest request, long userId, CancellationToken ct)
    {
        if (request.Validate().Count > 0) return new(null, 400, "Invalid payment request.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var contract = await db.Contracts.SingleOrDefaultAsync(x => x.ContractId == request.ContractId, ct);
            if (contract is null) return new(null, 404, "Contract not found.");
            if (contract.Status != ContractStatus.Active) return new(null, 409, "Payments require an Active contract.");
            if (!await db.Users.AnyAsync(x => x.UserId == userId && x.IsActive, ct))
                return new(null, 403, "Payment receiver is unavailable.");
            if (await db.Payments.AnyAsync(x => x.PaymentReference == request.PaymentReference.Trim(), ct))
                return new(null, 409, "Payment reference already exists.");

            var installments = await db.Installments.Where(x => x.ContractId == request.ContractId)
                .OrderBy(x => x.DueDate).ThenBy(x => x.InstallmentNumber).ToListAsync(ct);
            // Fail closed on inconsistent legacy balances; never silently repair financial data.
            if (installments.Any(x => x.Amount <= 0 || x.PaidAmount < 0 || x.RemainingAmount < 0 ||
                    x.PaidAmount + x.RemainingAmount != x.Amount ||
                    (x.Status == InstallmentStatus.Paid && x.RemainingAmount != 0)) ||
                installments.Sum(x => x.RemainingAmount) != contract.RemainingAmount)
                return new(null, 409, "Contract and installment balances do not reconcile.");
            var targets = installments.Where(x => x.RemainingAmount > 0 &&
                x.Status is InstallmentStatus.Pending or InstallmentStatus.PartiallyPaid or InstallmentStatus.Overdue).ToList();
            var outstanding = targets.Sum(x => x.RemainingAmount);
            if (outstanding <= 0) return new(null, 409, "Contract has no payable outstanding balance.");
            if (request.Amount > outstanding) return new(null, 409, "Payment amount exceeds the payable outstanding balance.");

            var payment = new Payment
            {
                ContractId = contract.ContractId, ReceivedByUserId = userId,
                PaymentReference = request.PaymentReference.Trim(), Amount = request.Amount,
                PaymentDate = request.PaymentDate?.UtcDateTime ?? DateTime.UtcNow,
                PaymentMethod = request.PaymentMethod.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);
            var unallocated = payment.Amount;
            foreach (var installment in targets)
            {
                if (unallocated == 0) break;
                var amount = decimal.Min(unallocated, installment.RemainingAmount);
                payment.PaymentAllocations.Add(new PaymentAllocation { Installment = installment, AllocatedAmount = amount });
                installment.PaidAmount += amount;
                installment.RemainingAmount -= amount;
                installment.Status = installment.RemainingAmount == 0 ? InstallmentStatus.Paid : InstallmentStatus.PartiallyPaid;
                unallocated -= amount;
            }
            if (unallocated != 0 || payment.PaymentAllocations.Sum(x => x.AllocatedAmount) != payment.Amount)
                throw new InvalidOperationException("Payment allocations do not reconcile.");
            contract.RemainingAmount -= payment.Amount;
            if (contract.RemainingAmount == 0) contract.Status = ContractStatus.Completed;
            await db.SaveChangesAsync(ct);
            var details = await GetAsync(payment.PaymentId, ct);
            await transaction.CommitAsync(ct);
            return new(details);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            // Npgsql can wrap serialization errors in InvalidOperationException ->
            // DbUpdateException -> PostgresException, rather than just one level.
            Exception? cause = ex;
            while (cause is not null && cause is not PostgresException) cause = cause.InnerException;
            var pg = cause as PostgresException;
            if (pg?.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation
                or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
                return new(null, 409, "Payment conflicted with existing or concurrently changed data. Review and retry using the same reference.");
            throw; // Existing API handler returns a generic 500 without database details.
        }
        // Disposal also rolls back early validation returns. No financial writes survive failure.
    }

    public Task<bool> ContractExistsAsync(long contractId, CancellationToken ct) =>
        db.Contracts.AsNoTracking().AnyAsync(x => x.ContractId == contractId, ct);

    public async Task<PaymentPage> ListAsync(string? search, long? contractId, long? customerId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Payments.AsNoTracking();
        if (contractId.HasValue) query = query.Where(x => x.ContractId == contractId.Value);
        if (customerId.HasValue) query = query.Where(x => x.Contract.CustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.PaymentReference.ToLower().Contains(term) || x.PaymentMethod.ToLower().Contains(term));
        }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.PaymentDate).ThenBy(x => x.PaymentId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(Summary).ToArray(), count, page, pageSize);
    }

    public async Task<PaymentDetails?> GetAsync(long id, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().Include(x => x.Contract).ThenInclude(x => x.Customer)
            .Include(x => x.PaymentAllocations).ThenInclude(x => x.Installment)
            .SingleOrDefaultAsync(x => x.PaymentId == id, ct);
        if (payment is null) return null;
        var c = payment.Contract;
        return new(Summary(payment), new(c.ContractId, c.ContractNumber, c.CustomerId, c.Customer.FullName, c.RemainingAmount, c.Status.ToString()),
            payment.PaymentAllocations.OrderBy(x => x.Installment.DueDate).ThenBy(x => x.Installment.InstallmentNumber)
                .Select(x => new PaymentAllocationResponse(x.PaymentAllocationId, x.AllocatedAmount, x.CreatedAt,
                    new InstallmentResponse(x.InstallmentId, x.Installment.InstallmentNumber, x.Installment.DueDate,
                        x.Installment.Amount, x.Installment.PaidAmount, x.Installment.RemainingAmount,
                        x.Installment.Status == InstallmentStatus.PartiallyPaid ? "Partially Paid" : x.Installment.Status.ToString()))).ToArray());
    }

    private static PaymentSummary Summary(Payment p) => new(p.PaymentId, p.PaymentReference, p.ContractId,
        p.ReceivedByUserId, p.PaymentDate, p.Amount, p.PaymentMethod, p.Notes);
}
