using System.Security.Claims;
using IMS.Application.Authentication;
using IMS.Application.Payments;

namespace IMS.API.Extensions;

public static class PaymentEndpointExtensions
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        string[] readers = [RoleNames.Admin, RoleNames.FinancialManager, RoleNames.CollectionOfficer, RoleNames.Auditor];
        var group = app.MapGroup("/api/payments")
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(readers));
        group.MapPost("", async (PaymentRequest request, ClaimsPrincipal user, IPaymentService service, CancellationToken ct) =>
        {
            var errors = request.Validate();
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (!long.TryParse(user.FindFirstValue("sub"), out var userId)) return Results.Unauthorized();
            var result = await service.CreateAsync(request, userId, ct);
            return result.ErrorStatus == 0
                ? Results.Created($"/api/payments/{result.Payment!.Payment.PaymentId}", result.Payment)
                : Results.Problem(statusCode: result.ErrorStatus, detail: result.Message);
        }).RequireAuthorization(p => p.RequireRole(RoleNames.Admin, RoleNames.FinancialManager, RoleNames.CollectionOfficer));
        group.MapGet("/{id:long}", async (long id, IPaymentService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } result ? Results.Ok(result) : Results.NotFound());
        group.MapGet("", async (IPaymentService service, CancellationToken ct, string? search = null,
            long? contractId = null, long? customerId = null, int page = 1, int pageSize = 50) =>
        {
            if (InvalidQuery(search, contractId, customerId, page, pageSize)) return InvalidQueryResponse();
            return Results.Ok(await service.ListAsync(search, contractId, customerId, page, pageSize, ct));
        });
        app.MapGet("/api/contracts/{contractId:long}/payments", async (long contractId, IPaymentService service,
            CancellationToken ct, int page = 1, int pageSize = 50) =>
        {
            if (InvalidQuery(null, contractId, null, page, pageSize)) return InvalidQueryResponse();
            if (!await service.ContractExistsAsync(contractId, ct)) return Results.NotFound();
            return Results.Ok(await service.ListAsync(null, contractId, null, page, pageSize, ct));
        }).RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(readers));
    }

    private static bool InvalidQuery(string? search, long? contractId, long? customerId, int page, int pageSize) =>
        page < 1 || pageSize < 1 || pageSize > 100 || page > int.MaxValue / pageSize ||
        contractId <= 0 || customerId <= 0 || search?.Length > 150 || search?.Contains('\0') == true;
    private static IResult InvalidQueryResponse() => Results.ValidationProblem(new Dictionary<string, string[]>
        { ["query"] = ["Invalid pagination, contract/customer ID or search (maximum 150 characters)."] });
}
