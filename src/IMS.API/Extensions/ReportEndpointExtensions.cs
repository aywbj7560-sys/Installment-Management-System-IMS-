using IMS.Application.Authentication;
using IMS.Application.Installments;
using IMS.Application.Reports;

namespace IMS.API.Extensions;

public static class ReportEndpointExtensions
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        string[] financial = [RoleNames.Admin, RoleNames.FinancialManager, RoleNames.Auditor];
        string[] collection = [.. financial, RoleNames.CollectionOfficer];
        var group = app.MapGroup("/api/reports").RequireAuthorization(p => p.RequireAuthenticatedUser());
        group.MapGet("/summary", async (IReportService service, CancellationToken ct) => Results.Ok(await service.SummaryAsync(ct)))
            .RequireAuthorization(p => p.RequireRole(financial));
        group.MapGet("/contracts", async ([AsParameters] ReportQuery query, IReportService service, CancellationToken ct) =>
        {
            var errors = query.Validate(false);
            return errors.Count > 0 ? Results.ValidationProblem(errors) : Results.Ok(await service.ContractsAsync(query, ct));
        }).RequireAuthorization(p => p.RequireRole(financial));
        group.MapGet("/payments", async ([AsParameters] ReportQuery query, IReportService service, CancellationToken ct) =>
        {
            var errors = query.Validate(true);
            return errors.Count > 0 ? Results.ValidationProblem(errors) : Results.Ok(await service.PaymentsAsync(query, ct));
        }).RequireAuthorization(p => p.RequireRole(financial));
        group.MapGet("/outstanding", async ([AsParameters] InstallmentQuery query, IReportService service, CancellationToken ct) =>
        {
            var errors = query.Validate(true);
            return errors.Count > 0 ? Results.ValidationProblem(errors) : Results.Ok(await service.OutstandingAsync(query, ct));
        }).RequireAuthorization(p => p.RequireRole(collection));
        group.MapGet("/customers/{customerId:long}/statement", async (long customerId, [AsParameters] StatementQuery query, IReportService service, CancellationToken ct) =>
        {
            var errors = query.Validate();
            if (customerId <= 0) errors["customerId"] = ["ID must be positive."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return await service.CustomerStatementAsync(customerId, query, ct) is { } result ? Results.Ok(result) : Results.NotFound();
        }).RequireAuthorization(p => p.RequireRole(collection));
        group.MapGet("/contracts/{contractId:long}/statement", async (long contractId, [AsParameters] StatementQuery query, IReportService service, CancellationToken ct) =>
        {
            var errors = query.Validate();
            if (contractId <= 0) errors["contractId"] = ["ID must be positive."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return await service.ContractStatementAsync(contractId, query, ct) is { } result ? Results.Ok(result) : Results.NotFound();
        }).RequireAuthorization(p => p.RequireRole(collection));
    }
}
