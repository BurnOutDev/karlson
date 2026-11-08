using System.Security.Cryptography;

namespace Karlson.Api.Domain;

public static class DietTypes
{
    public const string Normal = "normal";
    public const string Glp1 = "glp1";
    public const string Keto = "keto";
    public const string Kid = "kid";
    public const string Vegetarian = "vegetarian";
    public const string Vegan = "vegan";
    public const string GlutenFree = "gluten_free";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
        { Normal, Glp1, Keto, Kid, Vegetarian, Vegan, GlutenFree };
}

/// <summary>
/// A group of people who eat and shop together. Aggregate root: members are only changed
/// through it. The invite code is how a device joins the household (no user accounts yet).
/// </summary>
public class Household
{
    private readonly List<Member> _members = [];

    private Household() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string InviteCode { get; private set; } = "";
    public decimal? WeeklyBudgetGel { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<Member> Members => _members;

    public static Household Create(string name)
    {
        name = RequireName(name);
        return new Household
        {
            Id = Guid.NewGuid(),
            Name = name,
            InviteCode = NewInviteCode(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Rename(string name) => Name = RequireName(name);

    public void SetWeeklyBudget(decimal? gel)
    {
        if (gel is < 0)
            throw new DomainException("Budget cannot be negative.");
        WeeklyBudgetGel = gel;
    }

    public Member AddMember(string name, string dietType, string? notes)
    {
        if (_members.Count >= 12)
            throw new DomainException("A household can have at most 12 members.");

        var member = new Member(Guid.NewGuid(), Id, RequireName(name), RequireDiet(dietType), notes?.Trim());
        _members.Add(member);
        return member;
    }

    public void UpdateMember(Guid memberId, string name, string dietType, string? notes)
    {
        var member = _members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new DomainException("Member not found.");
        member.Update(RequireName(name), RequireDiet(dietType), notes?.Trim());
    }

    public void RemoveMember(Guid memberId)
    {
        if (_members.RemoveAll(m => m.Id == memberId) == 0)
            throw new DomainException("Member not found.");
    }

    public void RegenerateInviteCode() => InviteCode = NewInviteCode();

    private static string RequireName(string name)
    {
        name = name?.Trim() ?? "";
        if (name.Length is < 1 or > 60)
            throw new DomainException("Name must be 1-60 characters.");
        return name;
    }

    private static string RequireDiet(string dietType)
    {
        dietType = dietType?.Trim().ToLowerInvariant() ?? "";
        if (!DietTypes.All.Contains(dietType))
            throw new DomainException($"Unknown diet type '{dietType}'. Use one of: {string.Join(", ", DietTypes.All)}.");
        return dietType;
    }

    /// <summary>Six characters, no ambiguous letters (0/O, 1/I), easy to read out loud.</summary>
    private static string NewInviteCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Create(6, alphabet, (span, a) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = a[RandomNumberGenerator.GetInt32(a.Length)];
        });
    }
}

public class Member
{
    private Member() { }

    internal Member(Guid id, Guid householdId, string name, string dietType, string? notes)
    {
        Id = id;
        HouseholdId = householdId;
        Name = name;
        DietType = dietType;
        Notes = notes;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = "";

    /// <summary>One of <see cref="DietTypes"/>.</summary>
    public string DietType { get; private set; } = DietTypes.Normal;

    /// <summary>Free text the planner sees: allergies, dislikes, "no pork".</summary>
    public string? Notes { get; private set; }

    internal void Update(string name, string dietType, string? notes)
    {
        Name = name;
        DietType = dietType;
        Notes = notes;
    }
}

public class DomainException(string message) : Exception(message);
