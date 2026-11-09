using System.Net;
using System.Net.Http.Json;
using Karlson.Api.Features.Households;

namespace Karlson.Api.Tests.Http;

[Collection(ApiCollection.Name)]
public class HouseholdApiTests(ApiFixture api)
{
    private static async Task<HouseholdDto> Read(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HouseholdDto>())!;
    }

    private static async Task<HouseholdDto> CreateHousehold(HttpClient device, string name = "Test family") =>
        await Read(await device.PostAsJsonAsync("/api/households", new CreateHouseholdRequest(name)));

    [Fact]
    public async Task A_device_that_never_joined_is_unauthorized()
    {
        var device = api.NewDevice();

        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await device.PostAsJsonAsync("/api/me/members", new MemberRequest("Irakli", "normal", null))).StatusCode);
    }

    [Fact]
    public async Task Creating_a_household_signs_the_device_in()
    {
        var device = api.NewDevice();

        var created = await CreateHousehold(device, "Murusidze");
        var me = await Read(await device.GetAsync("/api/me"));

        Assert.Equal(created.Id, me.Id);
        Assert.Equal("Murusidze", me.Name);
        Assert.Equal(6, me.InviteCode.Length);
    }

    [Fact]
    public async Task A_second_device_joins_with_the_invite_code_and_sees_the_same_household()
    {
        var first = api.NewDevice();
        var second = api.NewDevice();
        var created = await CreateHousehold(first);
        await Read(await first.PostAsJsonAsync("/api/me/members", new MemberRequest("Irakli", "glp1", "no pork")));

        var joined = await Read(await second.PostAsJsonAsync("/api/households/join",
            new JoinHouseholdRequest($" {created.InviteCode.ToLowerInvariant()} ")));

        Assert.Equal(created.Id, joined.Id);
        var member = Assert.Single((await Read(await second.GetAsync("/api/me"))).Members);
        Assert.Equal(("Irakli", "glp1", "no pork"), (member.Name, member.DietType, member.Notes));
    }

    [Fact]
    public async Task An_unknown_invite_code_is_not_found_and_does_not_sign_in()
    {
        var device = api.NewDevice();

        var response = await device.PostAsJsonAsync("/api/households/join", new JoinHouseholdRequest("ZZZZZZ"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Members_and_budget_are_saved()
    {
        var device = api.NewDevice();
        await CreateHousehold(device);

        var withKid = await Read(await device.PostAsJsonAsync("/api/me/members", new MemberRequest("Kid", "kid", null)));
        var kid = Assert.Single(withKid.Members);
        await Read(await device.PutAsJsonAsync($"/api/me/members/{kid.Id}", new MemberRequest("Nika", "kid", "no fish")));
        await Read(await device.PutAsJsonAsync("/api/me", new UpdateHouseholdRequest("Renamed", 350m)));

        var me = await Read(await device.GetAsync("/api/me"));
        Assert.Equal(("Renamed", 350m), (me.Name, me.WeeklyBudgetGel));
        Assert.Equal(("Nika", "no fish"), (me.Members[0].Name, me.Members[0].Notes));

        var afterRemove = await Read(await device.DeleteAsync($"/api/me/members/{kid.Id}"));
        Assert.Empty(afterRemove.Members);
    }

    [Fact]
    public async Task A_domain_rule_violation_is_a_bad_request()
    {
        var device = api.NewDevice();
        await CreateHousehold(device);

        var response = await device.PostAsJsonAsync("/api/me/members", new MemberRequest("Irakli", "carnivore", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_new_invite_code_replaces_the_old_one()
    {
        var owner = api.NewDevice();
        var created = await CreateHousehold(owner);

        var regenerated = await Read(await owner.PostAsync("/api/me/invite-code", null));

        Assert.NotEqual(created.InviteCode, regenerated.InviteCode);
        var late = api.NewDevice();
        Assert.Equal(HttpStatusCode.NotFound,
            (await late.PostAsJsonAsync("/api/households/join", new JoinHouseholdRequest(created.InviteCode))).StatusCode);
    }

    [Fact]
    public async Task Leaving_signs_the_device_out_but_keeps_the_household()
    {
        var device = api.NewDevice();
        var created = await CreateHousehold(device);

        Assert.Equal(HttpStatusCode.NoContent, (await device.PostAsync("/api/households/leave", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/me")).StatusCode);

        var back = await Read(await device.PostAsJsonAsync("/api/households/join", new JoinHouseholdRequest(created.InviteCode)));
        Assert.Equal(created.Id, back.Id);
    }
}
