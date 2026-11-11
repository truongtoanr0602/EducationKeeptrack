using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTests;

public sealed class ServiceAccessMigrationTests
{
    [Fact]
    public async Task Existing_development_grants_keep_access_after_upgrade()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        var activeId = await app.AddTeacher("legacy-active@example.test", approved: false);
        var expiredId = await app.AddTeacher("legacy-expired@example.test", approved: false);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServiceAccessDatabase>();
            await db.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync("20261016163334_InitialServiceAccess");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO service_access."DevelopmentGrants" ("AccountId", "Approved", "Until")
                VALUES ({activeId}, true, {DateTimeOffset.UtcNow.AddDays(10)}), ({expiredId}, true, {DateTimeOffset.UtcNow.AddDays(-1)})
                """);
            await db.Database.MigrateAsync();
        }
        foreach (var (email, status) in new[] { ("legacy-active@example.test", "active"), ("legacy-expired@example.test", "expired") })
        {
            using var client = app.Client();
            await TestApplication.Login(client, email);
            var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal(status, me.GetProperty("access").GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/classes")).StatusCode);
        }
    }
}
