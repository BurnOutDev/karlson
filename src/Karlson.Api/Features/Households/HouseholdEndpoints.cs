using Karlson.Api.Data;
using Karlson.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Karlson.Api.Features.Households;

public record CreateHouseholdRequest(string Name);
public record JoinHouseholdRequest(string InviteCode);
public record UpdateHouseholdRequest(string Name, decimal? WeeklyBudgetGel);
public record MemberRequest(string Name, string DietType, string? Notes);

public record MemberDto(Guid Id, string Name, string DietType, string? Notes);
public record HouseholdDto(Guid Id, string Name, string InviteCode, decimal? WeeklyBudgetGel, IReadOnlyList<MemberDto> Members)
{
    public static HouseholdDto From(Household h) =>
        new(h.Id, h.Name, h.InviteCode, h.WeeklyBudgetGel, h.Members.Select(m => new MemberDto(m.Id, m.Name, m.DietType, m.Notes)).ToList());
}

/// <summary>
/// Vertical slice: create or join a household, manage its members and budget.
/// Commands load the aggregate, call one method, save. Queries project to DTOs.
/// </summary>
public static class HouseholdEndpoints
{
    public static IEndpointRouteBuilder MapHouseholds(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapPost("/households", Create);
        group.MapPost("/households/join", Join);
        group.MapPost("/households/leave", Leave);

        group.MapGet("/me", Me);
        group.MapPut("/me", Update);
        group.MapPost("/me/members", AddMember);
        group.MapPut("/me/members/{memberId:guid}", UpdateMember);
        group.MapDelete("/me/members/{memberId:guid}", RemoveMember);
        group.MapPost("/me/invite-code", RegenerateInviteCode);

        return app;
    }

    private static async Task<Ok<HouseholdDto>> Create(CreateHouseholdRequest request, AppDbContext db, HouseholdSession session, CancellationToken ct)
    {
        var household = Household.Create(request.Name);
        db.Households.Add(household);
        await db.SaveChangesAsync(ct);
        session.SignIn(household);
        return TypedResults.Ok(HouseholdDto.From(household));
    }

    private static async Task<Results<Ok<HouseholdDto>, NotFound<string>>> Join(JoinHouseholdRequest request, AppDbContext db, HouseholdSession session, CancellationToken ct)
    {
        var code = request.InviteCode.Trim().ToUpperInvariant();
        var household = await db.Households.FirstOrDefaultAsync(h => h.InviteCode == code, ct);
        if (household is null)
            return TypedResults.NotFound("No household has that invite code.");
        session.SignIn(household);
        return TypedResults.Ok(HouseholdDto.From(household));
    }

    private static NoContent Leave(HouseholdSession session)
    {
        session.SignOut();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<HouseholdDto>, UnauthorizedHttpResult>> Me(HouseholdSession session, CancellationToken ct) =>
        await session.CurrentHouseholdAsync(ct) is { } h ? TypedResults.Ok(HouseholdDto.From(h)) : TypedResults.Unauthorized();

    private static Task<Results<Ok<HouseholdDto>, UnauthorizedHttpResult, BadRequest<string>>> Update(UpdateHouseholdRequest request, AppDbContext db, HouseholdSession session, CancellationToken ct) =>
        Command(db, session, ct, h =>
        {
            h.Rename(request.Name);
            h.SetWeeklyBudget(request.WeeklyBudgetGel);
        });

    private static Task<Results<Ok<HouseholdDto>, UnauthorizedHttpResult, BadRequest<string>>> AddMember(MemberRequest request, AppDbContext db, HouseholdSession session, CancellationToken ct) =>
        Command(db, session, ct, h => h.AddMember(request.Name, request.DietType, request.Notes));

    private static Task<Results<Ok<HouseholdDto>, UnauthorizedHttpResult, BadRequest<string>>> UpdateMember(Guid memberId, MemberRequest request, AppDbContext db, HouseholdSession session, CancellationToken ct) =>
        Command(db, session, ct, h => h.UpdateMember(memberId, request.Name, request.DietType, request.Notes));

    private static Task<Results<Ok<HouseholdDto>, UnauthorizedHttpResult, BadRequest<string>>> RemoveMember(Guid memberId, AppDbContext db, HouseholdSession session, CancellationToken ct) =>
        Command(db, session, ct, h => h.RemoveMember(memberId));

    private static Task<Results<Ok<HouseholdDto>, UnauthorizedHttpResult, BadRequest<string>>> RegenerateInviteCode(AppDbContext db, HouseholdSession session, CancellationToken ct) =>
        Command(db, session, ct, h => h.RegenerateInviteCode());

    /// <summary>Load the current household, apply one domain action, save, return the new state.</summary>
    private static async Task<Results<Ok<HouseholdDto>, UnauthorizedHttpResult, BadRequest<string>>> Command(
        AppDbContext db, HouseholdSession session, CancellationToken ct, Action<Household> action)
    {
        var household = await session.CurrentHouseholdAsync(ct);
        if (household is null)
            return TypedResults.Unauthorized();

        try
        {
            action(household);
        }
        catch (DomainException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(HouseholdDto.From(household));
    }
}
