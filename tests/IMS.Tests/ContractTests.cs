using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.API.Extensions;
using IMS.Application.Authentication;
using IMS.Application.Contracts;
using IMS.Domain.Entities;
using IMS.Infrastructure.Authentication;
using IMS.Infrastructure.Contracts;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace IMS.Tests;

public class ContractTests
{
    private static async Task<WebApplication> Host()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "contract-test-only-signing-key-at-least-32-bytes", ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests"
        });
        builder.Services.AddImsAuthentication(builder.Configuration);
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ImsDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddScoped<IContractService, StubContractService>();
        var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapContractEndpoints();
        await app.StartAsync();
        return app;
    }

    private static HttpClient Client(WebApplication app, string? role)
    {
        var client = app.GetTestClient();
        if (role is not null)
        {
            var token = app.Services.GetRequiredService<JwtTokenGenerator>().Create(new User
            { UserId = 1, Email = "contract-tests@example.com", Role = new Role { RoleName = role } });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        return client;
    }



    [Theory]
    [InlineData(null,401,401)]
    [InlineData(RoleNames.Admin,200,201)]
    [InlineData(RoleNames.FinancialManager,200,201)]
    [InlineData(RoleNames.SalesAgent,200,201)]
    [InlineData(RoleNames.CollectionOfficer,200,403)]
    [InlineData(RoleNames.Auditor,200,403)]
    [InlineData("Unknown",403,403)]
    public async Task Roles(string? role,int read,int write)
    {
        await using var app=await Host(); using var client=Client(app,role);
        Assert.Equal(read,(int)(await client.GetAsync("/api/contracts")).StatusCode);
        Assert.Equal(read,(int)(await client.GetAsync("/api/contracts/1")).StatusCode);
        Assert.Equal(write,(int)(await client.PostAsJsonAsync("/api/contracts",Valid())).StatusCode);
    }
    public static ContractRequest Valid()=>new() { ContractNumber="test",CustomerId=1,ContractDate=new DateTimeOffset(2028,1,31,0,0,0,TimeSpan.Zero),Items=[new(1,1,100m)],GuarantorId=1 };
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task InvalidBody(string body)
    {
        await using var app=await Host();using var client=Client(app,RoleNames.Admin);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/contracts",new StringContent(body,System.Text.Encoding.UTF8,"application/json"))).StatusCode);
    }
    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("status=0")]
    [InlineData("status=Unknown")]
    [InlineData("customerId=0")]
    public async Task InvalidQuery(string query)
    {
        await using var app=await Host();using var client=Client(app,RoleNames.Admin);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/contracts?"+query)).StatusCode);
    }
    [Fact]
    public void ValidationAndSchedule()
    {
        Assert.Empty(Valid().Validate());
        foreach(var (name,value) in new (string,object?)[] { ("GuarantorId",null),("Items",null),("Items",new List<ContractItemRequest?> { new(1,0,1) }),("Items",new List<ContractItemRequest?> { new(1,1,-1) }),("Items",new List<ContractItemRequest?> { new(1,1,1.001m) }),("Items",new List<ContractItemRequest?> { new(1,1,1),new(1,1,1) }),("ContractNumber",new string('x',51)),("ContractDate",null),("DownPayment",-1m) })
        { var r=Valid();typeof(ContractRequest).GetProperty(name)!.SetValue(r,value);Assert.NotEmpty(r.Validate()); }
        Assert.NotEmpty(ContractValidation.Fields(new NewGuarantorRequest()));
        foreach(var amount in new[] {0.12m,100m,9999999999999.99m})
        { var schedule=ContractValidation.Schedule(amount);Assert.Equal(12,schedule.Length);Assert.Equal(amount,schedule.Sum());Assert.All(schedule,x=>Assert.True(x>0));Assert.All(schedule.Take(11),x=>Assert.Equal(schedule[0],x)); }
        Assert.Equal(8.37m,ContractValidation.Schedule(100m)[11]);
    }
}
public sealed class StubContractService : IContractService
{
    private static ContractDetails Details()=>new(new(1,"test",1,"Customer",1,DateTime.UtcNow,100,0,100,12,"Draft",DateTime.UtcNow),[],[],[]);
    public Task<ContractResult> CreateAsync(ContractRequest r,long userId,CancellationToken ct)=>Task.FromResult(new ContractResult(Details()));
    public Task<ContractDetails?> GetAsync(long id,CancellationToken ct)=>Task.FromResult<ContractDetails?>(Details());
    public Task<ContractPage> ListAsync(string? search,long? customerId,IMS.Domain.Enums.ContractStatus? status,int page,int pageSize,CancellationToken ct)=>Task.FromResult(new ContractPage([],0,page,pageSize));
}
