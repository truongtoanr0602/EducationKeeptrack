using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed partial class TuitionTests
{
    [Fact]
    public async Task Debt_list_spans_the_owners_classes_tracks_receipts_and_hides_other_teachers()
    {
        var (client, classId, studentId) = await PaymentSetup();
        var (other, otherClass, _) = await PaymentSetup();
        using (client) using (other)
        {
            var first = await client.GetFromJsonAsync<JsonElement>("/api/billing/debts?month=2026-10");
            Assert.Equal("500000", first.GetProperty("totalDebtVnd").GetString());
            Assert.Equal(1, first.GetProperty("count").GetInt32());
            var item = first.GetProperty("items")[0];
            Assert.Equal(classId, item.GetProperty("classId").GetString());
            Assert.Equal(studentId, item.GetProperty("studentId").GetString());
            Assert.Equal("500000", item.GetProperty("totalDebtVnd").GetString());
            Assert.DoesNotContain(first.GetProperty("classes").EnumerateArray(), x => x.GetProperty("classId").GetString() == otherClass);

            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            Assert.Equal(version, item.GetProperty("version").GetInt64());
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments", new { studentId, amountVnd = "300000", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            var partial = await client.GetFromJsonAsync<JsonElement>($"/api/billing/debts?month=2026-10&classId={classId}");
            Assert.Equal("200000", partial.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("200000", partial.GetProperty("items")[0].GetProperty("totalDebtVnd").GetString());

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/billing/debts?month=2026-10&classId={otherClass}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/billing/debts?month=2026-10&pageSize=0")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/billing/debts?month=2026-10&page=0")).StatusCode);
        }
    }

    [Fact]
    public async Task Debt_list_requires_a_signed_in_teacher()
    {
        using var anonymous = app.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/billing/debts?month=2026-10")).StatusCode);
    }
}
