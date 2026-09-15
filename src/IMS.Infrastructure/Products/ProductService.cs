using IMS.Application.Products;
using IMS.Domain.Entities;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IMS.Infrastructure.Products;

public sealed class ProductService(ImsDbContext db) : IProductService
{
    public async Task<ProductPage> ListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Products.AsNoTracking();
        if (isActive.HasValue) query = query.Where(p => p.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.ProductCode.ToLower().Contains(term)
                || (p.Description != null && p.Description.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(cancellationToken);
        var products = await query.OrderBy(p => p.ProductId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(products.Select(Response).ToArray(), total, page, pageSize);
    }

    public async Task<ProductResponse?> GetAsync(long id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.ProductId == id, cancellationToken);
        return product is null ? null : Response(product);
    }

    public async Task<ProductWriteResult> SaveAsync(long? id, ProductRequest request, CancellationToken cancellationToken)
    {
        var product = id.HasValue
            ? await db.Products.SingleOrDefaultAsync(p => p.ProductId == id.Value, cancellationToken) : new Product();
        if (product is null) return new(null, ProductWriteError.NotFound);
        var code = request.ProductCode.Trim();
        if (await db.Products.AnyAsync(p => p.ProductCode == code && p.ProductId != product.ProductId, cancellationToken))
            return new(null, ProductWriteError.DuplicateCode);
        product.ProductCode = code;
        product.Name = request.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        product.CashPrice = request.CashPrice!.Value;
        product.InstallmentPrice = request.InstallmentPrice;
        product.IsActive = request.IsActive;
        if (!id.HasValue) db.Products.Add(product);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(product).State = EntityState.Detached;
            return new(null, ProductWriteError.NotFound);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "products_product_code_key" })
        {
            db.Entry(product).State = EntityState.Detached;
            return new(null, ProductWriteError.DuplicateCode);
        }
        return new(Response(product));
    }

    private static ProductResponse Response(Product p) => new(p.ProductId, p.ProductCode, p.Name, p.Description,
        p.CashPrice, p.InstallmentPrice, p.IsActive, p.CreatedAt);
}
