using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace EducationKeeptrack.Reporting.Infrastructure;

public sealed class DownloadTokens(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("Reporting.MonthlyPdf.Download.v1");
    public string Issue(string owner,Guid exportId,DateTimeOffset expiresAt) => protector.Protect(JsonSerializer.Serialize(new Claim(owner,exportId,expiresAt)));
    public bool Validate(string token,string owner,Guid exportId,DateTimeOffset now)
    {
        if (token.Length > 4096) return false;
        try { var claim=JsonSerializer.Deserialize<Claim>(protector.Unprotect(token)); return claim is not null && claim.Owner==owner && claim.ExportId==exportId && claim.ExpiresAt>now; }
        catch (Exception e) when(e is CryptographicException or JsonException or FormatException) { return false; }
    }
    private sealed record Claim(string Owner,Guid ExportId,DateTimeOffset ExpiresAt);
}
