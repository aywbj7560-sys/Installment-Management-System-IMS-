using IMS.Application.Customers;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IMS.Infrastructure.Customers;

public sealed class CustomerService(ImsDbContext db) : ICustomerService
{
    public async Task<CustomerPage> ListAsync(string? search, CustomerStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Customers.AsNoTracking();
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            long.TryParse(term, out var id);
            query = query.Where(c => c.FullName.ToLower().Contains(term) || c.Phone.Contains(term)
                || (c.SecondaryPhone != null && c.SecondaryPhone.Contains(term))
                || c.IdentificationNumber.ToLower().Contains(term)
                || (c.Email != null && c.Email.ToLower().Contains(term)) || c.CustomerId == id);
        }
        var total = await query.CountAsync(cancellationToken);
        var customers = await query.OrderBy(c => c.CustomerId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(customers.Select(Response).ToArray(), total, page, pageSize);
    }

    public async Task<CustomerResponse?> GetAsync(long id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.CustomerId == id, cancellationToken);
        return customer is null ? null : Response(customer);
    }

    public async Task<CustomerWriteResult> SaveAsync(long? id, CustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = id.HasValue
            ? await db.Customers.SingleOrDefaultAsync(c => c.CustomerId == id.Value, cancellationToken) : new Customer();
        if (customer is null) return new(null, CustomerWriteError.NotFound);
        var identification = request.IdentificationNumber.Trim();
        var name = request.FullName.Trim();
        if (id.HasValue && (customer.IdentificationNumber != identification || customer.FullName != name))
            return new(null, CustomerWriteError.ImmutableIdentity);
        if (await db.Customers.AnyAsync(c => c.IdentificationNumber == identification && c.CustomerId != customer.CustomerId, cancellationToken))
            return new(null, CustomerWriteError.DuplicateIdentification);
        customer.FullName = name;
        customer.IdentificationNumber = identification;
        customer.Phone = request.Phone.Trim();
        customer.SecondaryPhone = Optional(request.SecondaryPhone);
        customer.Email = Optional(request.Email);
        customer.Address = request.Address.Trim();
        customer.Status = Enum.Parse<CustomerStatus>(request.Status);
        if (!id.HasValue) db.Customers.Add(customer);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "customers_identification_number_key" })
        {
            db.Entry(customer).State = EntityState.Detached;
            return new(null, CustomerWriteError.DuplicateIdentification);
        }
        return new(Response(customer));
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static CustomerResponse Response(Customer c) => new(c.CustomerId, c.FullName, c.IdentificationNumber,
        c.Phone, c.SecondaryPhone, c.Email, c.Address, c.Status.ToString(), c.CreatedAt);
}
