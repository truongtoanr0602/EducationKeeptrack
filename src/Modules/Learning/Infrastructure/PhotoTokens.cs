using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class PhotoTokens(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("Learning.SubmissionPhoto.Download.v1");

    public string Issue(string owner, Guid photoId, DateTimeOffset expiresAt) =>
        protector.Protect(JsonSerializer.Serialize(new Claim(owner, photoId, expiresAt)));

    public (bool Valid, string Owner, Guid PhotoId) Validate(string token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096)
            return (false, "", Guid.Empty);
        try
        {
            var claim = JsonSerializer.Deserialize<Claim>(protector.Unprotect(token));
            if (claim is not null && claim.ExpiresAt > now)
                return (true, claim.Owner, claim.PhotoId);
            return (false, "", Guid.Empty);
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
        {
            return (false, "", Guid.Empty);
        }
    }

    private sealed record Claim(string Owner, Guid PhotoId, DateTimeOffset ExpiresAt);
}
