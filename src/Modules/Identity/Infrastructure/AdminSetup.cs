using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Identity.Infrastructure;

public sealed record AdminSetupResult(string AccountId, string AuthenticatorKey, string OtpAuthUri);

// Operational tool only (run with --setup-admin and secrets from the environment).
// There is deliberately no HTTP endpoint that creates or promotes an Admin.
public static class AdminSetup
{
    public const string Role = "Admin";

    public static async Task<AdminSetupResult> RunAsync(IServiceProvider services, string email, string password)
    {
        var users = services.GetRequiredService<UserManager<Account>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (await users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("Account already exists; Admin setup never promotes or resets an existing account.");
        if (!await roles.RoleExistsAsync(Role)) Check(await roles.CreateAsync(new IdentityRole(Role)));
        var account = new Account { UserName = email, Email = email, EmailConfirmed = true };
        Check(await users.CreateAsync(account, password));
        Check(await users.AddToRoleAsync(account, Role));
        Check(await users.ResetAuthenticatorKeyAsync(account));
        Check(await users.SetTwoFactorEnabledAsync(account, true));
        var key = (await users.GetAuthenticatorKeyAsync(account))!;
        var uri = $"otpauth://totp/{Uri.EscapeDataString("Education Keeptrack")}:{Uri.EscapeDataString(email)}?secret={key}&issuer={Uri.EscapeDataString("Education Keeptrack")}&digits=6";
        return new(account.Id, key, uri);
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
