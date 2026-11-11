using System.Security.Cryptography;
using System.Text;
using EducationKeeptrack.Identity.Domain;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Identity.Infrastructure;

public sealed class EmailTokens(IdentityDatabase db, TimeProvider clock)
{
    public async Task<string> IssueAsync(string accountId, EmailTokenPurpose purpose, TimeSpan lifetime, string? newEmail, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // A new link supersedes older unused links of the same purpose.
        await db.EmailTokens.Where(x => x.AccountId == accountId && x.Purpose == purpose && x.UsedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.ExpiresAt, now), ct);
        var raw = Base64Url(RandomNumberGenerator.GetBytes(32));
        db.EmailTokens.Add(new EmailToken
        {
            Id = Guid.NewGuid(), AccountId = accountId, Purpose = purpose, TokenHash = Hash(raw),
            NewEmail = newEmail, CreatedAt = now, ExpiresAt = now + lifetime
        });
        await db.SaveChangesAsync(ct);
        return raw;
    }

    // Atomically marks the token used; a used, superseded or expired token never succeeds again.
    public async Task<EmailToken?> ConsumeAsync(string? raw, EmailTokenPurpose purpose, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 100) return null;
        var hash = Hash(raw);
        var now = clock.GetUtcNow();
        var claimed = await db.EmailTokens.Where(x => x.TokenHash == hash && x.Purpose == purpose && x.UsedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.UsedAt, now), ct);
        return claimed == 1 ? await db.EmailTokens.AsNoTracking().SingleAsync(x => x.TokenHash == hash, ct) : null;
    }

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
