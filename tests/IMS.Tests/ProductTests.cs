using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.API.Extensions;
using IMS.Application.Authentication;
using IMS.Application.Products;
using IMS.Domain.Entities;
using IMS.Infrastructure.Authentication;
using IMS.Infrastructure.Products;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace IMS.Tests;

public class ProductTests
{
    private static async Task<WebApplication> Host()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "product-test-only-signing-key-at-least-32-bytes", ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests"
        });
        builder.Services.AddImsAuthentication(builder.Configuration);
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ImsDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddScoped<IProductService, ProductService>();
        var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapProductEndpoints();
        await app.StartAsync();
        return app;
    }

    private static HttpClient Client(WebApplication app, string? role)
    {
        var client = app.GetTestClient();
        if (role is not null)
        {
            var token = app.Services.GetRequiredService<JwtTokenGenerator>().Create(new User
            { UserId = 1, Email = "product-tests@example.com", Role = new Role { RoleName = role } });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        return client;
    }


    private static ProductRequest Request(string code = "SKU-123", bool active = true) => new()
    { ProductCode = code, Name = "Test Refrigerator", Description = "Cooling appliance", CashPrice = 125.50m, InstallmentPrice = 150m, IsActive = active };

    [Theory]
    [InlineData(null, 401, 401)]
    [InlineData(RoleNames.Admin, 200, 201)]
    [InlineData(RoleNames.FinancialManager, 200, 201)]
    [InlineData(RoleNames.SalesAgent, 200, 403)]
    [InlineData(RoleNames.CollectionOfficer, 200, 403)]
    [InlineData(RoleNames.Auditor, 200, 403)]
    [InlineData("Unknown", 403, 403)]
    public async Task EveryEndpointEnforcesRoles(string? role, int read, int write)
    {
        await using var app = await Host(); using var admin = Client(app, RoleNames.Admin);
        var created = await admin.PostAsJsonAsync("/api/products", Request());
        using var client = Client(app, role);
        Assert.Equal(read, (int)(await client.GetAsync("/api/products")).StatusCode);
        Assert.Equal(read, (int)(await client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(write, (int)(await client.PostAsJsonAsync("/api/products", Request("other"))).StatusCode);
        Assert.Equal(write == 201 ? 200 : write, (int)(await client.PutAsJsonAsync(created.Headers.Location, Request(active: false))).StatusCode);
    }

    [Fact]
    public async Task CreateUpdateReadPaginationSearchAndConflicts()
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        var created = await client.PostAsJsonAsync("/api/products", Request());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = (await created.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.Equal($"/api/products/{product.ProductId}", created.Headers.Location!.ToString());
        Assert.True(product.IsActive); Assert.Equal(125.50m, product.CashPrice); Assert.Equal(150m, product.InstallmentPrice);
        Assert.DoesNotContain("contractItems", await created.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/products", Request())).StatusCode);
        var second = await client.PostAsJsonAsync("/api/products", Request("second", false));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(second.Headers.Location, Request())).StatusCode);
        var page1 = (await client.GetFromJsonAsync<ProductPage>("/api/products?pageSize=1"))!;
        var page2 = (await client.GetFromJsonAsync<ProductPage>("/api/products?pageSize=1&page=2"))!;
        Assert.Equal(2, page1.TotalCount); Assert.Single(page1.Items); Assert.Single(page2.Items);
        Assert.True(page1.Items[0].ProductId < page2.Items[0].ProductId);
        Assert.Empty((await client.GetFromJsonAsync<ProductPage>("/api/products?pageSize=1&page=3"))!.Items);
        foreach (var search in new[] { "REFRIGERATOR", "cooling", "sku-123" })
            Assert.Contains((await client.GetFromJsonAsync<ProductPage>("/api/products?search=" + search))!.Items, p => p.ProductId == product.ProductId);
        foreach (var search in new[] { "missing", "%25", "%5F" })
            Assert.Empty((await client.GetFromJsonAsync<ProductPage>("/api/products?search=" + search))!.Items);
        Assert.Single((await client.GetFromJsonAsync<ProductPage>("/api/products?isActive=false"))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<ProductPage>("/api/products?search=sku-123&isActive=false"))!.Items);
        var updated = await client.PutAsJsonAsync(created.Headers.Location, new ProductRequest
        { ProductCode = "changed", Name = "New name", CashPrice = 0, InstallmentPrice = null, IsActive = false });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var saved = (await client.GetFromJsonAsync<ProductResponse>(created.Headers.Location))!;
        Assert.Equal("changed", saved.ProductCode); Assert.Equal("New name", saved.Name);
        Assert.False(saved.IsActive); Assert.Equal(0m, saved.CashPrice); Assert.Null(saved.InstallmentPrice); Assert.Null(saved.Description);
        Assert.Equal(product.CreatedAt, saved.CreatedAt);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/products/99999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/products/99999", Request())).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"productCode\":\"x\",\"name\":\"x\"}")]
    [InlineData("{\"productCode\":\"x\",\"name\":\"x\",\"cashPrice\":-1}")]
    [InlineData("{\"productCode\":\"x\",\"name\":\"x\",\"cashPrice\":1,\"installmentPrice\":-1}")]
    [InlineData("{\"productCode\":\"x\",\"name\":\"x\",\"cashPrice\":1.001}")]
    [InlineData("{\"productCode\":\"x\",\"name\":\"x\",\"cashPrice\":10000000000000}")]
    [InlineData("{\"productCode\":\"x\",\"name\":\"x\",\"cashPrice\":1,\"isActive\":\"Active\"}")]
    [InlineData("{\"productCode\":\"x\",\"name\":\"x\",\"cashPrice\":1,\"isActive\":null}")]
    [InlineData("{\"productCode\":\"x\",\"name\":\"x\",\"cashPrice\":1,\"isActive\":2}")]
    public async Task InvalidBodiesReturn400ForCreateAndUpdate(string body)
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/products", content)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/products/1", content)).StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("page=invalid")]
    [InlineData("isActive=Active")]
    [InlineData("isActive=1")]
    public async Task InvalidQueriesReturn400(string query)
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/products?" + query)).StatusCode);
    }

    [Fact]
    public void ValidationCoversLengthsRequiredFieldsAndNumericBoundaries()
    {
        Assert.Empty(new ProductRequest { ProductCode = new string('x',50), Name = new string('x',150), CashPrice = 9999999999999.99m, InstallmentPrice = 0 }.Validate());
        foreach (var (property, value) in new[] { ("ProductCode", new string('x',51)), ("Name",new string('x',151)), ("Name"," "), ("ProductCode"," "), ("Description","a\0b") })
        {
            var request = Request(); typeof(ProductRequest).GetProperty(property)!.SetValue(request,value);
            Assert.Contains(property, request.Validate().Keys);
        }
        foreach (var price in new[] { -1m, 0.001m, 10000000000000m })
            Assert.Contains("InstallmentPrice", new ProductRequest { ProductCode="x", Name="x", CashPrice=0, InstallmentPrice=price }.Validate().Keys);
    }

    [Fact]
    public async Task ReadOperationsDoNotTrackAndRespectCancellation()
    {
        await using var db = new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Products.Add(new Product { ProductCode="x", Name="x" }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = new ProductService(db);
        await service.GetAsync(1,default); await service.ListAsync(null,null,1,50,default);
        Assert.Empty(db.ChangeTracker.Entries());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ListAsync(null,null,1,50,cancellation.Token));
    }
}
