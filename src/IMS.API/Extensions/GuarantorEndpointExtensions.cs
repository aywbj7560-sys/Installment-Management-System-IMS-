using IMS.Application.Authentication;
using IMS.Application.Guarantors;

namespace IMS.API.Extensions;

public static class GuarantorEndpointExtensions
{
    public static void MapGuarantorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/guarantors").RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(RoleNames.All.ToArray()));
        group.MapGet("", async (IGuarantorService service, CancellationToken ct, string? search = null,
            bool? isActive = null, int page = 1, int pageSize = 50) =>
        {
            if (page < 1 || pageSize < 1 || pageSize > 100 || page > int.MaxValue / pageSize || search?.Length > 150 || search?.Contains('\0') == true)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = ["Use a positive page, pageSize 1-100 and search up to 150 characters without null characters."] });
            return Results.Ok(await service.ListAsync(search, isActive, page, pageSize, ct));
        });
        group.MapGet("/{id:long}", async (long id, IGuarantorService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } result ? Results.Ok(result) : Results.NotFound());
        group.MapPost("", async (GuarantorRequest request, IGuarantorService service, CancellationToken ct) =>
            await Save(null, request, service, ct)).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.FinancialManager, RoleNames.SalesAgent));
        group.MapPut("/{id:long}", async (long id, GuarantorRequest request, IGuarantorService service, CancellationToken ct) =>
            await Save(id, request, service, ct)).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.FinancialManager, RoleNames.SalesAgent));
    }

    private static async Task<IResult> Save(long? id, GuarantorRequest request, IGuarantorService service, CancellationToken ct)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await service.SaveAsync(id, request, ct);
        if (result.ErrorStatus != 0) return Results.Problem(statusCode: result.ErrorStatus, detail: result.Message);
        return id.HasValue ? Results.Ok(result.Guarantor) : Results.Created($"/api/guarantors/{result.Guarantor!.GuarantorId}", result.Guarantor);
    }
}
