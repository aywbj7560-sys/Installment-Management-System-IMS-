using IMS.Application.Authentication;
using IMS.Application.Products;

namespace IMS.API.Extensions;

public static class ProductEndpointExtensions
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/products").RequireAuthorization(policy =>
            policy.RequireAuthenticatedUser().RequireRole(RoleNames.All.ToArray()));
        group.MapGet("", async (IProductService service, CancellationToken cancellationToken,
            string? search = null, bool? isActive = null, int page = 1, int pageSize = 50) =>
        {
            if (page < 1 || pageSize < 1 || pageSize > 100 || page > int.MaxValue / pageSize || search?.Length > 150 || search?.Contains('\0') == true)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = ["Use a positive page, pageSize 1-100, and search up to 150 characters without null characters."] });
            return Results.Ok(await service.ListAsync(search, isActive, page, pageSize, cancellationToken));
        });
        group.MapGet("/{id:long}", async (long id, IProductService service, CancellationToken cancellationToken) =>
            await service.GetAsync(id, cancellationToken) is { } product ? Results.Ok(product) : Results.NotFound());
        group.MapPost("", async (ProductRequest request, IProductService service, CancellationToken cancellationToken) =>
            await Save(null, request, service, cancellationToken)).RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin, RoleNames.FinancialManager));
        group.MapPut("/{id:long}", async (long id, ProductRequest request, IProductService service, CancellationToken cancellationToken) =>
            await Save(id, request, service, cancellationToken)).RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin, RoleNames.FinancialManager));
    }

    private static async Task<IResult> Save(long? id, ProductRequest request, IProductService service, CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await service.SaveAsync(id, request, cancellationToken);
        return result.Error switch
        {
            ProductWriteError.NotFound => Results.NotFound(),
            ProductWriteError.DuplicateCode => Results.Conflict(new { message = "A product with this product code already exists." }),
            _ => id.HasValue ? Results.Ok(result.Product) : Results.Created($"/api/products/{result.Product!.ProductId}", result.Product)
        };
    }
}
