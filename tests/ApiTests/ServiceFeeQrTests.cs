using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.ServiceAccess.Application;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTests;

// The Admin's static bank QR for the service fee (#51): stored with the payment instructions and shown to teachers.
public sealed class ServiceFeeQrTests(TestApplication app) : IClassFixture<TestApplication>
{
    private const string Path = "/api/admin/payment-instructions";

    // A real 1x1 PNG whose colour differs per call, so replacing one image with another is observable.
    internal static string Png(byte red)
    {
        static byte[] BigEndian(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
        static byte[] Chunk(string type, byte[] data)
        {
            var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
            var crc = ~body.Aggregate(0xFFFFFFFFu, (c, b) =>
            {
                c ^= b;
                for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320u : c >> 1;
                return c;
            });
            return BigEndian((uint)data.Length).Concat(body).Concat(BigEndian(crc)).ToArray();
        }
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true)) z.Write([0, red, 0, 0, 255]);
        byte[] header = [0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0];
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.Concat(Chunk("IHDR", header)).Concat(Chunk("IDAT", raw.ToArray())).Concat(Chunk("IEND", [])).ToArray();
        return Convert.ToBase64String(png);
    }

    private static string DataUrl(string base64) => "data:image/png;base64," + base64;

    private async Task<HttpClient> Admin(string email)
    {
        var key = await app.AddAdmin(email);
        var client = app.Client();
        await app.LoginAdmin(client, email, key);
        return client;
    }

    private static async Task<JsonElement> Current(HttpClient admin) => await admin.GetFromJsonAsync<JsonElement>(Path);

    private static async Task<HttpResponseMessage> Save(HttpClient admin, uint version, string? image, string bank = "NH A - STK 0001")
    {
        // A text-only save leaves the property out of the body, like an older client.
        var body = new Dictionary<string, object?> { ["bankDetails"] = bank, ["contact"] = "Zalo 0900000000", ["version"] = version };
        if (image is not null) body["staticQrBase64"] = image;
        return await TestApplication.Put(admin, Path, body);
    }

    private static async Task<uint> Version(HttpClient admin) => (await Current(admin)).GetProperty("version").GetUInt32();

    private async Task<JsonElement> TeacherView(string email)
    {
        await app.AddTeacher(email);
        using var teacher = app.Client();
        await TestApplication.Login(teacher, email);
        return (await teacher.GetFromJsonAsync<JsonElement>("/api/service")).GetProperty("instructions");
    }

    [Fact]
    public async Task Admin_saves_replaces_keeps_and_clears_the_qr_and_teachers_see_it()
    {
        using var admin = await Admin("qr-admin@example.test");
        var first = Png(10);
        var saved = await Save(admin, await Version(admin), first);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var view = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(view.GetProperty("hasStaticQr").GetBoolean());
        Assert.Equal(DataUrl(first), view.GetProperty("staticQrDataUrl").GetString());

        var seen = await TeacherView("qr-teacher-1@example.test");
        Assert.Equal(DataUrl(first), seen.GetProperty("qrDataUrl").GetString());
        Assert.Contains("STK 0001", seen.GetProperty("bankDetails").GetString());

        // Leaving the property out keeps the stored image, so editing text never loses the QR.
        var textOnly = await Save(admin, await Version(admin), image: null, bank: "NH B - STK 0002");
        Assert.Equal(HttpStatusCode.OK, textOnly.StatusCode);
        Assert.Equal(DataUrl(first), (await Current(admin)).GetProperty("staticQrDataUrl").GetString());
        Assert.Contains("STK 0002", (await Current(admin)).GetProperty("bankDetails").GetString());

        var second = Png(200);
        Assert.Equal(HttpStatusCode.OK, (await Save(admin, await Version(admin), second)).StatusCode);
        Assert.Equal(DataUrl(second), (await Current(admin)).GetProperty("staticQrDataUrl").GetString());

        Assert.Equal(HttpStatusCode.OK, (await Save(admin, await Version(admin), "", bank: "NH B - STK 0002")).StatusCode);
        var cleared = await Current(admin);
        Assert.False(cleared.GetProperty("hasStaticQr").GetBoolean());
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("staticQrDataUrl").ValueKind);
        var after = await TeacherView("qr-teacher-2@example.test");
        Assert.Equal(JsonValueKind.Null, after.GetProperty("qrDataUrl").ValueKind);
        Assert.Contains("STK 0002", after.GetProperty("bankDetails").GetString());
    }

    [Fact]
    public async Task Teacher_sees_text_only_instructions_when_no_qr_was_ever_saved()
    {
        using var admin = await Admin("noqr-admin@example.test");
        Assert.Equal(HttpStatusCode.OK, (await Save(admin, await Version(admin), image: null)).StatusCode);
        var seen = await TeacherView("noqr-teacher@example.test");
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("qrDataUrl").ValueKind);
        Assert.Contains("STK 0001", seen.GetProperty("bankDetails").GetString());
    }

    [Theory]
    [InlineData("not base64 !!")]
    [InlineData("bm90IGEgcG5nIGF0IGFsbCwganVzdCB0ZXh0IHRoYXQgaXMgbG9uZyBlbm91Z2ggdG8gcGFzcyB0aGUgbGVuZ3RoIGNoZWNr")]
    public async Task Invalid_images_are_rejected_without_touching_the_stored_qr(string bad)
    {
        using var admin = await Admin($"bad-{Math.Abs(bad.GetHashCode())}@example.test");
        var good = Png(33);
        Assert.Equal(HttpStatusCode.OK, (await Save(admin, await Version(admin), good)).StatusCode);

        var rejected = await Save(admin, await Version(admin), bad);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("PNG", await rejected.Content.ReadAsStringAsync());
        Assert.Equal(DataUrl(good), (await Current(admin)).GetProperty("staticQrDataUrl").GetString());
    }

    [Fact]
    public async Task Oversized_image_is_refused_by_the_server_not_only_by_the_request_limit()
    {
        using var admin = await Admin("big-admin@example.test");
        var tooBig = new string('A', ServiceQrImage.MaxBase64Length + 4);
        var response = await Save(admin, await Version(admin), tooBig);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("80 KB", await response.Content.ReadAsStringAsync());
        Assert.False((await Current(admin)).GetProperty("hasStaticQr").GetBoolean());
    }

    [Fact]
    public async Task Stale_version_conflicts_and_leaves_the_qr_unchanged()
    {
        using var admin = await Admin("race-admin@example.test");
        var stale = await Version(admin);
        var kept = Png(77);
        Assert.Equal(HttpStatusCode.OK, (await Save(admin, stale, kept)).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await Save(admin, stale, Png(78))).StatusCode);
        Assert.Equal(DataUrl(kept), (await Current(admin)).GetProperty("staticQrDataUrl").GetString());
    }

    [Fact]
    public async Task Teachers_cannot_change_the_qr()
    {
        await app.AddTeacher("qr-intruder@example.test");
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "qr-intruder@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await Save(teacher, 0, Png(1))).StatusCode);
    }

    [Fact]
    public async Task Every_change_is_recorded_in_history_with_what_happened_to_the_qr()
    {
        using var admin = await Admin("history-admin@example.test");
        Assert.Equal(HttpStatusCode.OK, (await Save(admin, await Version(admin), Png(5))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Save(admin, await Version(admin), image: null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Save(admin, await Version(admin), "")).StatusCode);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceAccessDatabase>();
        var entries = await db.History.AsNoTracking()
            .Where(x => x.Action == "instructions.updated" && x.ActorEmail == "history-admin@example.test")
            .OrderBy(x => x.At).ToListAsync();
        Assert.Equal(3, entries.Count);
        Assert.Contains("replaced", entries[0].Details);
        Assert.Contains("unchanged", entries[1].Details);
        Assert.Contains("removed", entries[2].Details);
    }
}
