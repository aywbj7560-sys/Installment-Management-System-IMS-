using IMS.Application.Guarantors;
using IMS.Domain.Entities;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IMS.Infrastructure.Guarantors;

public sealed class GuarantorService(ImsDbContext db) : IGuarantorService
{
    public async Task<GuarantorPage> ListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Guarantors.AsNoTracking();
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            long.TryParse(term, out var id);
            query = query.Where(x => x.GuarantorId == id || x.FullName.ToLower().Contains(term) ||
                x.IdentificationNumber.ToLower().Contains(term) || x.Phone.Contains(term) ||
                x.SecondaryPhone != null && x.SecondaryPhone.Contains(term));
        }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.GuarantorId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(Response).ToArray(), count, page, pageSize);
    }

    public async Task<GuarantorDetails?> GetAsync(long id, CancellationToken ct)
    {
        var guarantor = await db.Guarantors.AsNoTracking().Include(x => x.ContractGuarantors).ThenInclude(x => x.Contract)
            .SingleOrDefaultAsync(x => x.GuarantorId == id, ct);
        return guarantor is null ? null : new(Response(guarantor), guarantor.ContractGuarantors.OrderBy(x => x.ContractId)
            .Select(x => new GuarantorContractSummary(x.ContractGuarantorId, x.ContractId, x.Contract.ContractNumber,
                x.Contract.Status.ToString(), x.Notes, x.CreatedAt)).ToArray());
    }

    public async Task<GuarantorResult> SaveAsync(long? id, GuarantorRequest request, CancellationToken ct)
    {
        if (request.Validate().Count > 0) return new(null, 400, "Invalid guarantor request.");
        var guarantor = id.HasValue ? await db.Guarantors.SingleOrDefaultAsync(x => x.GuarantorId == id, ct) : new Guarantor();
        if (guarantor is null) return new(null, 404, "Guarantor not found.");
        var name = request.FullName.Trim();
        var identification = request.IdentificationNumber.Trim();
        if (id.HasValue && (guarantor.FullName != name || guarantor.IdentificationNumber != identification))
            return new(null, 409, "Guarantor name and identification number cannot be changed.");
        if (await db.Guarantors.AsNoTracking().AnyAsync(x => x.IdentificationNumber == identification && x.GuarantorId != guarantor.GuarantorId, ct))
            return new(null, 409, "Guarantor identification already exists.");
        guarantor.FullName = name;
        guarantor.IdentificationNumber = identification;
        guarantor.Phone = request.Phone.Trim();
        guarantor.SecondaryPhone = Optional(request.SecondaryPhone);
        guarantor.Address = request.Address.Trim();
        guarantor.Occupation = Optional(request.Occupation);
        guarantor.Workplace = Optional(request.Workplace);
        guarantor.Notes = Optional(request.Notes);
        guarantor.IsActive = request.IsActive ?? guarantor.IsActive;
        if (!id.HasValue) db.Guarantors.Add(guarantor);
        try
        {
            // A single SaveChanges atomically writes the profile only; links are never loaded for writes.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "guarantors_identification_number_key" })
        {
            db.Entry(guarantor).State = EntityState.Detached;
            return new(null, 409, "Guarantor identification already exists.");
        }
        return new(Response(guarantor));
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static GuarantorResponse Response(Guarantor x) => new(x.GuarantorId, x.FullName, x.IdentificationNumber,
        x.Phone, x.SecondaryPhone, x.Address, x.Occupation, x.Workplace, x.Notes, x.IsActive, x.CreatedAt);
}
