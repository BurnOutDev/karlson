using Karlson.Api.Domain;

namespace Karlson.Api.Tests;

public class HouseholdTests
{
    [Fact]
    public void Create_sets_a_readable_six_character_invite_code()
    {
        var household = Household.Create("  Murusidze family ");

        Assert.Equal("Murusidze family", household.Name);
        Assert.Equal(6, household.InviteCode.Length);
        Assert.DoesNotContain(household.InviteCode, c => "0O1I".Contains(c));
        Assert.Empty(household.Members);
    }

    [Fact]
    public void Members_are_added_updated_and_removed_through_the_aggregate()
    {
        var household = Household.Create("Test");

        var irakli = household.AddMember("Irakli", "GLP1", " no pork ");
        household.AddMember("Kid", DietTypes.Kid, null);

        Assert.Equal(2, household.Members.Count);
        Assert.Equal(DietTypes.Glp1, irakli.DietType);
        Assert.Equal("no pork", irakli.Notes);

        household.UpdateMember(irakli.Id, "Irakli M.", DietTypes.Keto, null);
        Assert.Equal(DietTypes.Keto, household.Members.Single(m => m.Id == irakli.Id).DietType);

        household.RemoveMember(irakli.Id);
        Assert.Single(household.Members);
    }

    [Theory]
    [InlineData("")]
    [InlineData("paleo")]
    public void Rejects_unknown_diet_types(string diet) =>
        Assert.Throws<DomainException>(() => Household.Create("Test").AddMember("A", diet, null));

    [Fact]
    public void Rejects_negative_budget_and_unknown_members()
    {
        var household = Household.Create("Test");

        Assert.Throws<DomainException>(() => household.SetWeeklyBudget(-1));
        Assert.Throws<DomainException>(() => household.RemoveMember(Guid.NewGuid()));

        household.SetWeeklyBudget(250);
        Assert.Equal(250m, household.WeeklyBudgetGel);
    }
}
