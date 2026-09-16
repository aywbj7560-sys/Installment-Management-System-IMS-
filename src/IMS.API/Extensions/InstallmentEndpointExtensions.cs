using IMS.Application.Authentication;
using IMS.Application.Installments;

namespace IMS.API.Extensions;

public static class InstallmentEndpointExtensions
{
    public static void MapInstallmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/installments").RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(RoleNames.All.ToArray()));
        group.MapGet("", async ([AsParameters] InstallmentQuery query, IInstallmentService service, CancellationToken ct) =>
            await List(query, false, service, ct));
        group.MapGet("/{id:long}", async (long id, IInstallmentService service, CancellationToken ct) =>
        {
            if (id <= 0) return InvalidId();
            return await service.GetAsync(id, ct) is { } result ? Results.Ok(result) : Results.NotFound();
        });
        app.MapGet("/api/contracts/{contractId:long}/installments", async (long contractId, IInstallmentService service, CancellationToken ct) =>
        {
            if (contractId <= 0) return InvalidId();
            return await service.ScheduleAsync(contractId, ct) is { } result ? Results.Ok(result) : Results.NotFound();
        }).RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(RoleNames.All.ToArray()));
        app.MapGet("/api/collections/due", async ([AsParameters] InstallmentQuery query, IInstallmentService service, CancellationToken ct) =>
            await List(query, true, service, ct))
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(RoleNames.Admin, RoleNames.FinancialManager, RoleNames.CollectionOfficer));
    }

    private static async Task<IResult> List(InstallmentQuery query, bool collectionQueue, IInstallmentService service, CancellationToken ct)
    {
        var errors = query.Validate(collectionQueue);
        return errors.Count > 0 ? Results.ValidationProblem(errors) : Results.Ok(await service.ListAsync(query, collectionQueue, ct));
    }

    private static IResult InvalidId() => Results.ValidationProblem(new Dictionary<string, string[]> { ["id"] = ["ID must be positive."] });
}
