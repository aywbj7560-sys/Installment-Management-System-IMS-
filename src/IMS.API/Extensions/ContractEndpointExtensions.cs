using System.Security.Claims;
using IMS.Application.Authentication;
using IMS.Application.Contracts;
using IMS.Domain.Enums;
namespace IMS.API.Extensions;
public static class ContractEndpointExtensions
{
    public static void MapContractEndpoints(this WebApplication app)
    {
        var group=app.MapGroup("/api/contracts").RequireAuthorization(p=>p.RequireAuthenticatedUser().RequireRole(RoleNames.All.ToArray()));
        group.MapPost("",async (ContractRequest request,ClaimsPrincipal user,IContractService service,CancellationToken ct)=>
        {
            var errors=request.Validate();
            if(errors.Count>0) return Results.ValidationProblem(errors);
            if(!long.TryParse(user.FindFirstValue("sub"),out var userId)) return Results.Unauthorized();
            var result=await service.CreateAsync(request,userId,ct);
            return result.ErrorStatus==0 ? Results.Created($"/api/contracts/{result.Contract!.Contract.ContractId}",result.Contract) : Results.Problem(statusCode:result.ErrorStatus,detail:result.Message);
        }).RequireAuthorization(p=>p.RequireRole(RoleNames.Admin,RoleNames.FinancialManager,RoleNames.SalesAgent));
        group.MapGet("/{id:long}",async(long id,IContractService service,CancellationToken ct)=>await service.GetAsync(id,ct) is {} result ? Results.Ok(result):Results.NotFound());
        group.MapGet("",async(IContractService service,CancellationToken ct,string? search=null,long? customerId=null,string? status=null,int page=1,int pageSize=50)=>
        {
            ContractStatus? filter=null;
            if(status is not null) { if(!Enum.TryParse<ContractStatus>(status,out var parsed)||!Enum.IsDefined(parsed)||parsed.ToString()!=status) return Results.ValidationProblem(new Dictionary<string,string[]> { ["status"]=["Invalid contract status."] }); filter=parsed; }
            if(page<1||pageSize<1||pageSize>100||page>int.MaxValue/pageSize||customerId<=0||search?.Length>150||search?.Contains('\0')==true) return Results.ValidationProblem(new Dictionary<string,string[]> { ["query"]=["Invalid pagination, customer ID or search (maximum 150 characters)."] });
            return Results.Ok(await service.ListAsync(search,customerId,filter,page,pageSize,ct));
        });
    }
}
