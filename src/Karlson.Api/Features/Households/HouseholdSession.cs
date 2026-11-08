using Karlson.Api.Data;
using Karlson.Api.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Karlson.Api.Features.Households;

/// <summary>
/// Which household this browser belongs to. A device joins with an invite code and gets a
/// protected cookie holding the household id; that is the whole "auth" for private validation.
/// </summary>
public class HouseholdSession(IHttpContextAccessor accessor, IDataProtectionProvider protection, AppDbContext db)
{
    public const string CookieName = "karlson_household";
    private readonly IDataProtector _protector = protection.CreateProtector("karlson.household-cookie.v1");

    private HttpContext Http => accessor.HttpContext ?? throw new InvalidOperationException("No HTTP context.");

    public Guid? CurrentHouseholdId
    {
        get
        {
            if (!Http.Request.Cookies.TryGetValue(CookieName, out var value))
                return null;
            try
            {
                return Guid.Parse(_protector.Unprotect(value));
            }
            catch
            {
                return null; // tampered or from an older key: treat as signed out
            }
        }
    }

    public async Task<Household?> CurrentHouseholdAsync(CancellationToken ct) =>
        CurrentHouseholdId is { } id ? await db.Households.FirstOrDefaultAsync(h => h.Id == id, ct) : null;

    public void SignIn(Household household) =>
        Http.Response.Cookies.Append(CookieName, _protector.Protect(household.Id.ToString()), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Http.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Path = "/",
        });

    public void SignOut() => Http.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
}
