using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

/// <summary>Which saved PDFs are outdated after the data changes (#22). Needs the R2 test bucket like the other export tests.</summary>
public sealed class ExportFreshnessTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static async Task<JsonElement[]> List(HttpClient client, string path) =>
        (await client.GetFromJsonAsync<JsonElement>(path + "/exports")).EnumerateArray().ToArray();

    private static string State(JsonElement export) => export.GetProperty("freshness").GetProperty("state").GetString()!;
    private static string[] Areas(JsonElement export) =>
        export.GetProperty("freshness").GetProperty("changes").EnumerateArray().Select(x => x.GetProperty("area").GetString()!).Distinct().Order().ToArray();

    private static async Task<(HttpClient Client, string Path, Guid ClassId, Guid StudentId, JsonElement First)> SetupWithExport(TestApplication app)
    {
        var (client, path, classId, studentId, _) = await ReportingFixture.Setup(app);
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "500000", version = 0 })).StatusCode);
        await ReportingFixture.Session(client, classId, studentId, "2026-10-02");
        await ReportingFixture.Payments(client, classId);
        Assert.Equal(HttpStatusCode.OK, (await ReportingFixture.Put(client, path + "/draft", ReportingFixture.Draft())).StatusCode);
        var (_, first) = await ReportDownloadTests.ExportAndLink(client, path, 1);
        return (client, path, classId, studentId, first);
    }

    [Fact]
    public async Task A_fresh_export_is_current_and_editing_the_draft_makes_it_outdated_without_touching_the_file()
    {
        var (client, path, _, _, first) = await SetupWithExport(app);
        using (client)
        {
            var before = Assert.Single(await List(client, path));
            Assert.Equal("current", State(before));
            Assert.Empty(before.GetProperty("freshness").GetProperty("changes").EnumerateArray());

            Assert.Equal(HttpStatusCode.OK, (await ReportingFixture.Put(client, path + "/draft", ReportingFixture.Draft(1, comment: "Đã sửa nhận xét"))).StatusCode);
            var after = Assert.Single(await List(client, path));
            Assert.Equal("outdated", State(after));
            Assert.Equal(["draft"], Areas(after));
            var change = after.GetProperty("freshness").GetProperty("changes")[0];
            Assert.Equal("Bản nháp 1", change.GetProperty("before").GetString());
            Assert.Equal("Bản nháp 2", change.GetProperty("after").GetString());
            // The saved file is unchanged and still downloadable with its original checksum.
            Assert.Equal(first.GetProperty("sha256").GetString(), after.GetProperty("sha256").GetString());
            var link = await (await TestApplication.Post(client, $"{path}/exports/{first.GetProperty("id").GetGuid()}/download-link", new { })).Content.ReadFromJsonAsync<JsonElement>();
            var bytes = await client.GetByteArrayAsync(link.GetProperty("url").GetString());
            Assert.Equal(first.GetProperty("sha256").GetString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
        }
    }

    [Fact]
    public async Task Attendance_fee_and_result_changes_are_reported_with_before_and_after_and_a_new_export_is_current()
    {
        var (client, path, classId, studentId, _) = await SetupWithExport(app);
        using (client)
        {
            await ReportingFixture.Session(client, classId, studentId, "2026-10-09");
            await ReportingFixture.Payments(client, classId);
            var items = await List(client, path);
            var stale = Assert.Single(items);
            Assert.Equal("outdated", State(stale));
            Assert.Equal(["attendance", "fees"], Areas(stale));
            var changes = stale.GetProperty("freshness").GetProperty("changes").EnumerateArray().ToArray();
            var attendance = changes.Single(x => x.GetProperty("area").GetString() == "attendance");
            Assert.Equal("1 buổi có mặt trên 1 buổi đã diễn ra", attendance.GetProperty("before").GetString());
            Assert.Equal("2 buổi có mặt trên 2 buổi đã diễn ra", attendance.GetProperty("after").GetString());
            var fees = changes.Single(x => x.GetProperty("area").GetString() == "fees" && x.GetProperty("label").GetString()!.Contains("phát sinh"));
            Assert.Equal("500.000 đồng", fees.GetProperty("before").GetString());
            Assert.Equal("1.000.000 đồng", fees.GetProperty("after").GetString());

            // A new export from current data is current; the old one stays listed as outdated, and nothing was replaced.
            var (_, second) = await ReportDownloadTests.ExportAndLink(client, path, 1);
            var after = await List(client, path);
            Assert.Equal(2, after.Length);
            Assert.Equal("current", State(after.Single(x => x.GetProperty("id").GetGuid() == second.GetProperty("id").GetGuid())));
            Assert.Equal("outdated", State(after.Single(x => x.GetProperty("id").GetGuid() != second.GetProperty("id").GetGuid())));

            // A confirmed assessment result entering the month makes the newest export outdated for that area only.
            var created = await (await TestApplication.Post(client, $"/api/learning/classes/{classId}/assessments", new { title = "Phân số", date = "2026-10-12", defaultMethod = "manual" })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(HttpStatusCode.OK, (await ReportingFixture.Put(client, $"/api/learning/classes/{classId}/assessments/{created.GetProperty("id").GetGuid()}/submissions/{studentId}",
                new { status = "assessed", completion = 75, correctness = 50, version = 1, confirm = true })).StatusCode);
            var withResult = (await List(client, path)).Single(x => x.GetProperty("id").GetGuid() == second.GetProperty("id").GetGuid());
            Assert.Equal("outdated", State(withResult));
            Assert.Equal(["results"], Areas(withResult));
            var result = withResult.GetProperty("freshness").GetProperty("changes")[0];
            Assert.Contains("Phân số", result.GetProperty("label").GetString());
            Assert.Equal("Chưa có kết quả được xác nhận", result.GetProperty("before").GetString());
            Assert.Equal("Hoàn thành 75%, làm đúng 50%", result.GetProperty("after").GetString());
        }
    }

    [Fact]
    public async Task Another_teacher_cannot_list_the_exports()
    {
        var (client, path, _, _, _) = await SetupWithExport(app);
        var (other, _, _, _, _) = await ReportingFixture.Setup(app);
        using (client)
        using (other)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "/exports")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + "/exports")).StatusCode);
        }
    }
}
