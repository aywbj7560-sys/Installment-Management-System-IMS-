using IMS.Application.Authentication;
using IMS.Application.Customers;
using IMS.Domain.Enums;

namespace IMS.API.Extensions;

public static class CustomerEndpointExtensions
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/customers").RequireAuthorization(policy =>
            policy.RequireAuthenticatedUser().RequireRole(RoleNames.All.ToArray()));
        group.MapGet("", async (ICustomerService service, CancellationToken cancellationToken,
            string? search = null, string? status = null, int page = 1, int pageSize = 50) =>
        {
            CustomerStatus? filter = null;
            if (status is not null)
            {
                if (!Enum.TryParse<CustomerStatus>(status, out var parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != status)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Use Active, Inactive, or Blacklisted."] });
                filter = parsed;
            }
            if (page < 1 || pageSize < 1 || pageSize > 100 || page > int.MaxValue / pageSize || search?.Length > 150 || search?.Contains('\0') == true)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = ["Use a positive page, pageSize 1-100, and search up to 150 characters without null characters."] });
            return Results.Ok(await service.ListAsync(search, filter, page, pageSize, cancellationToken));
        });
        group.MapGet("/{id:long}", async (long id, ICustomerService service, CancellationToken cancellationToken) =>
            await service.GetAsync(id, cancellationToken) is { } customer ? Results.Ok(customer) : Results.NotFound());
        group.MapPost("", async (CustomerRequest request, ICustomerService service, CancellationToken cancellationToken) =>
            await Save(null, request, service, cancellationToken)).RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin, RoleNames.FinancialManager, RoleNames.SalesAgent));
        group.MapPut("/{id:long}", async (long id, CustomerRequest request, ICustomerService service, CancellationToken cancellationToken) =>
            await Save(id, request, service, cancellationToken)).RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin, RoleNames.FinancialManager, RoleNames.SalesAgent));
    }

    private static async Task<IResult> Save(long? id, CustomerRequest request, ICustomerService service, CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await service.SaveAsync(id, request, cancellationToken);
        return result.Error switch
        {
            CustomerWriteError.NotFound => Results.NotFound(),
            CustomerWriteError.DuplicateIdentification => Results.Conflict(new { message = "A customer with this identification number already exists." }),
            CustomerWriteError.ImmutableIdentity => Results.Conflict(new { message = "Customer name and identification number cannot be changed." }),
            _ => id.HasValue ? Results.Ok(result.Customer) : Results.Created($"/api/customers/{result.Customer!.CustomerId}", result.Customer)
        };
    }
}
