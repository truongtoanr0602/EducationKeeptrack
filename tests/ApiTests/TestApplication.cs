using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Identity.Domain;
using EducationKeeptrack.Identity.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace ApiTests;

public sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan amount) => now += amount;
}

public sealed class TestApplication : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string adminConnection = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION")
        ?? throw new InvalidOperationException("TEST_DATABASE_CONNECTION must point to a real PostgreSQL test server with CREATEDB permission.");
    private readonly string database = "ekt_test_" + Guid.NewGuid().ToString("N");
    public TestClock Clock { get; } = new();
    public bool ProductionCookies { get; init; }
    public string Connection => new NpgsqlConnectionStringBuilder(adminConnection) { Database = database }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(ProductionCookies ? "Production" : "Testing");
        builder.UseSetting("ConnectionStrings:Database", Connection);
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
        await command.ExecuteNonQueryAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDatabase>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<EducationKeeptrack.Classroom.Infrastructure.ClassroomDatabase>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<EducationKeeptrack.ServiceAccess.Infrastructure.ServiceAccessDatabase>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string> AddTeacher(string email, bool approved = true, bool confirmed = true)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var account = new Account { UserName = email, Email = email, EmailConfirmed = confirmed };
        var result = await users.CreateAsync(account, "Test-only!Password123");
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(e => e.Description)));
        var grants = scope.ServiceProvider.GetRequiredService<EducationKeeptrack.ServiceAccess.Infrastructure.ServiceAccessDatabase>();
        grants.DevelopmentGrants.Add(new EducationKeeptrack.ServiceAccess.Domain.DevelopmentGrant { AccountId = account.Id, Approved = approved, Until = Clock.GetUtcNow().AddDays(30) });
        await grants.SaveChangesAsync();
        return account.Id;
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    public static async Task Login(HttpClient client, string email)
    {
        var response = await Post(client, "/api/auth/login", new { email, password = "Test-only!Password123" });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
