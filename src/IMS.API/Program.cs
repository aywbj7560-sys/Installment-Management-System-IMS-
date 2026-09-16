using IMS.API.Extensions;
using IMS.Infrastructure;
using IMS.Infrastructure.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Services.AddImsAuthentication(builder.Configuration);
var app = builder.Build();

// Always use a generic error response, including in Development; never expose DB details.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
}));
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<DevelopmentAdminSeeder>().SeedAsync();

app.MapGet("/", () => "IMS API is running");
app.MapDatabaseHealthEndpoint();
app.MapAuthenticationEndpoints();
app.MapCustomerEndpoints();
app.MapProductEndpoints();
app.MapContractEndpoints();
app.MapPaymentEndpoints();
app.MapGuarantorEndpoints();
app.MapInstallmentEndpoints();
app.MapReportEndpoints();

app.Run();
