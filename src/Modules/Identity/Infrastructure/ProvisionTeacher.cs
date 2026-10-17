using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Identity.Infrastructure;

public static class ProvisionTeacher
{
    // Deliberately no public registration/approval API in T01.
    public static async Task<string> RunAsync(IServiceProvider services, string email, string password)
    {
        var users = services.GetRequiredService<UserManager<Account>>();
        if (await users.FindByEmailAsync(email) is not null) throw new InvalidOperationException("Account already exists; provisioning never resets existing credentials.");
        var account = new Account { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(account, password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        return account.Id;
    }
}
