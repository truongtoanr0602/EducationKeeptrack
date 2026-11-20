using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace EducationKeeptrack.Learning.Infrastructure;

/// <summary>
/// Short-lived signed tokens of the question bank: image links (usable without a teacher session, so students can
/// see the pictures of their exam) and the student's exam session kept in an HttpOnly cookie.
/// </summary>
public sealed class BankTokens(IDataProtectionProvider provider)
{
    private readonly IDataProtector images = provider.CreateProtector("Learning.QuestionImage.Download.v1");
    private readonly IDataProtector seats = provider.CreateProtector("Learning.ExamSeat.Session.v1");

    public string IssueImage(Guid imageId, DateTimeOffset expiresAt) => images.Protect(JsonSerializer.Serialize(new ImageClaim(imageId, expiresAt)));

    public Guid? ValidateImage(string token, DateTimeOffset now)
    {
        var claim = Read<ImageClaim>(images, token);
        return claim is not null && claim.ExpiresAt > now ? claim.ImageId : null;
    }

    public string IssueSeat(Guid examId, Guid seatId, string nonce, DateTimeOffset expiresAt) =>
        seats.Protect(JsonSerializer.Serialize(new SeatClaim(examId, seatId, nonce, expiresAt)));

    public (Guid ExamId, Guid SeatId, string Nonce)? ValidateSeat(string? token, DateTimeOffset now)
    {
        var claim = Read<SeatClaim>(seats, token);
        return claim is not null && claim.ExpiresAt > now ? (claim.ExamId, claim.SeatId, claim.Nonce) : null;
    }

    private static T? Read<T>(IDataProtector protector, string? token) where T : class
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return null;
        try { return JsonSerializer.Deserialize<T>(protector.Unprotect(token)); }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { return null; }
    }

    private sealed record ImageClaim(Guid ImageId, DateTimeOffset ExpiresAt);
    private sealed record SeatClaim(Guid ExamId, Guid SeatId, string Nonce, DateTimeOffset ExpiresAt);
}
