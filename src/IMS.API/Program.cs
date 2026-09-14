using IMS.API.Extensions;
using IMS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("DefaultConnection"));
var app = builder.Build();

app.MapGet("/", () => "IMS API is running");
app.MapDatabaseHealthEndpoint();

app.Run();
