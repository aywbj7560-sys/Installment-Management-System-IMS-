using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.API.Extensions;
using IMS.Application.Authentication;
using IMS.Application.Guarantors;
using IMS.Domain.Entities;
using IMS.Infrastructure.Authentication;
using IMS.Infrastructure.Guarantors;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IMS.Tests;

public class GuarantorTests
{
    public static GuarantorRequest Request(string identity = "ID-1", bool? active = null) => new()
    {
        FullName = "Test Guarantor", IdentificationNumber = identity, Phone = "+964 770-1234567",
        SecondaryPhone = "07801234567", Address = "Baghdad", Occupation = "Teacher", Workplace = "School", Notes = "Profile note", IsActive = active
    };

    private static async Task<WebApplication> Host()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "guarantor-test-only-signing-key-at-least-32-bytes", ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests"
        });
        builder.Services.AddImsAuthentication(builder.Configuration);
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ImsDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddScoped<IGuarantorService, GuarantorService>();
        var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapGuarantorEndpoints();
        await app.StartAsync(); return app;
    }

    private static HttpClient Client(WebApplication app, string? role)
    {
        var client = app.GetTestClient();
        if (role is not null)
        {
            var token = app.Services.GetRequiredService<JwtTokenGenerator>().Create(new User
                { UserId = 1, Email = "guarantor@example.com", Role = new Role { RoleName = role } });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        return client;
    }

    [Theory]
    [InlineData(null, 401, 401)]
    [InlineData(RoleNames.Admin, 200, 201)]
    [InlineData(RoleNames.FinancialManager, 200, 201)]
    [InlineData(RoleNames.SalesAgent, 200, 201)]
    [InlineData(RoleNames.CollectionOfficer, 200, 403)]
    [InlineData(RoleNames.Auditor, 200, 403)]
    [InlineData("Unknown", 403, 403)]
    public async Task EveryEndpointEnforcesRoles(string? role, int read, int write)
    {
        await using var app = await Host(); using var admin = Client(app, RoleNames.Admin);
        var created = await admin.PostAsJsonAsync("/api/guarantors", Request());
        using var client = Client(app, role);
        Assert.Equal(read, (int)(await client.GetAsync("/api/guarantors")).StatusCode);
        Assert.Equal(read, (int)(await client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(write, (int)(await client.PostAsJsonAsync("/api/guarantors", Request("ID-2"))).StatusCode);
        Assert.Equal(write == 201 ? 200 : write, (int)(await client.PutAsJsonAsync(created.Headers.Location, Request(active: false))).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"email\":\"x@example.com\"}")]
    [InlineData("{\"contractGuarantors\":[]}")]
    [InlineData("{\"isActive\":\"yes\"}")]
    public async Task InvalidBody(string body)
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put })
        {
            using var request = new HttpRequestMessage(method, method == HttpMethod.Post ? "/api/guarantors" : "/api/guarantors/1")
                { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
        }
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647")]
    [InlineData("isActive=invalid")]
    [InlineData("search=%00")]
    public async Task InvalidQuery(string query)
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/guarantors?" + query)).StatusCode);
    }

    [Fact]
    public async Task ValidationFailuresDoNotPersist()
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        foreach (var (name, value) in new (string, object?)[] {
            ("FullName", " "), ("FullName", new string('x',151)), ("IdentificationNumber", ""),
            ("IdentificationNumber", new string('x',51)), ("Phone", ""), ("Phone", "call me"),
            ("Phone", new string('1',21)), ("SecondaryPhone", "12+34"), ("SecondaryPhone", new string('1',21)),
            ("Address", null), ("Occupation", new string('x',101)), ("Workplace", new string('x',151)), ("Notes", "bad\0note") })
        {
            var request = Request(); typeof(GuarantorRequest).GetProperty(name)!.SetValue(request, value);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/guarantors", request)).StatusCode);
        }
        Assert.Empty((await client.GetFromJsonAsync<GuarantorPage>("/api/guarantors"))!.Items);
    }

    [Fact]
    public async Task CrudIdentityAndOptionalStatus()
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        var created = await client.PostAsJsonAsync("/api/guarantors", Request());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var guarantor = (await created.Content.ReadFromJsonAsync<GuarantorResponse>())!;
        Assert.True(guarantor.IsActive);
        Assert.Empty((await client.GetFromJsonAsync<GuarantorDetails>(created.Headers.Location))!.Contracts);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/guarantors", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(created.Headers.Location, Request("different"))).StatusCode);
        var rename = Request(); typeof(GuarantorRequest).GetProperty("FullName")!.SetValue(rename, "Changed");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(created.Headers.Location, rename)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(created.Headers.Location, Request(active: false))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(created.Headers.Location, Request())).StatusCode);
        Assert.False((await client.GetFromJsonAsync<GuarantorDetails>(created.Headers.Location))!.Guarantor.IsActive);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(created.Headers.Location, Request(active: true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/guarantors/9223372036854775807")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/guarantors/9223372036854775807", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
    }
}
