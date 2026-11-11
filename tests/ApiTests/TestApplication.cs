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
using EducationKeeptrack.Contracts;

namespace ApiTests;

public sealed class TestClock : TimeProvider
{
    // Whole seconds so values round-trip PostgreSQL's microsecond precision exactly.
    private DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan amount) => now += amount;
}

public sealed class TestEmailOutbox : EducationKeeptrack.Identity.Application.IEmailSender
{
    private readonly List<EducationKeeptrack.Identity.Application.EmailMessage> messages = [];
    public IReadOnlyList<EducationKeeptrack.Identity.Application.EmailMessage> Messages { get { lock (messages) return [.. messages]; } }
    public Task SendAsync(EducationKeeptrack.Identity.Application.EmailMessage message, CancellationToken cancellationToken)
    {
        lock (messages) messages.Add(message);
        return Task.CompletedTask;
    }
}

public sealed class TestApplication : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string adminConnection = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION")
        ?? throw new InvalidOperationException("TEST_DATABASE_CONNECTION must point to a real PostgreSQL test server with CREATEDB permission.");
    private readonly string database = "ekt_test_" + Guid.NewGuid().ToString("N");
    private readonly bool reuseDatabase;
    private readonly bool ownsDatabase = true;
    private readonly ReportTestStorage reportStorage = new();
    public TestApplication() { }
    private TestApplication(TestApplication original, IClassroomChangeHandler? classroomHandler, ITuitionChangeHandler? tuitionHandler)
    {
        database = original.database; adminConnection = original.adminConnection; Clock = original.Clock; Outbox = original.Outbox;
        reuseDatabase = true; ownsDatabase = false; ProductionCookies = original.ProductionCookies;
        reportStorage = original.reportStorage;
        ClassroomChangeHandler = classroomHandler; TuitionChangeHandler = tuitionHandler;
    }
    public Task StopHostAsync() => base.DisposeAsync().AsTask();
    public TestApplication Reopen(IClassroomChangeHandler? classroomHandler = null, ITuitionChangeHandler? tuitionHandler = null) => new(this, classroomHandler, tuitionHandler);
    public TestApplication ReopenWithMigrationCheck() => new(this, null, null) { CheckPendingMigrations = true };
    public TestClock Clock { get; } = new();
    public TestEmailOutbox Outbox { get; } = new();
    public IReadOnlyList<EducationKeeptrack.Identity.Application.EmailMessage> Emails => Outbox.Messages;
    public bool ProductionCookies { get; init; }
    // The fixture starts the host before migrating its database, so the startup check is off by default.
    public bool CheckPendingMigrations { get; init; }
    public int ReportPreviewMinutes { get; init; } = 15;
    public IClassroomChangeHandler? ClassroomChangeHandler { get; init; }
    public Func<IClassroomChangeHandler, IClassroomChangeHandler>? DecorateBillingHandler { get; init; }
    public ITuitionChangeHandler? TuitionChangeHandler { get; init; }
    public ILearningResultChangeHandler? LearningResultChangeHandler { get; init; }
    public Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor? BillingInterceptor { get; init; }
    public Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor? ReportingInterceptor { get; init; }
    public EducationKeeptrack.Billing.Application.IVietQrGenerator? QrGenerator { get; init; }
    public Func<EducationKeeptrack.Reporting.Application.IReportObjectStore, EducationKeeptrack.Reporting.Application.IReportObjectStore>? DecorateReportStore { get; init; }
    public string Connection => new NpgsqlConnectionStringBuilder(adminConnection) { Database = database }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(ProductionCookies ? "Production" : "Testing");
        builder.UseSetting("ConnectionStrings:Database", Connection);
        builder.UseSetting("Startup:CheckPendingMigrations", CheckPendingMigrations ? "true" : "false");
        builder.UseSetting("Reporting:Endpoint", Environment.GetEnvironmentVariable("TEST_S3_ENDPOINT") ?? "");
        builder.UseSetting("Reporting:AccessKey", Environment.GetEnvironmentVariable("TEST_S3_ACCESS_KEY") ?? "");
        builder.UseSetting("Reporting:SecretKey", Environment.GetEnvironmentVariable("TEST_S3_SECRET_KEY") ?? "");
        builder.UseSetting("Reporting:Bucket", Environment.GetEnvironmentVariable("TEST_S3_BUCKET") ?? "");
        builder.UseSetting("Reporting:Region", "auto");
        builder.UseSetting("Reporting:PreviewMinutes", ReportPreviewMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<EducationKeeptrack.Identity.Application.IEmailSender>(Outbox);
            services.AddSingleton<EducationKeeptrack.Reporting.Application.IReportObjectStore>(_ =>
            {
                var store = reportStorage.CreateStore();
                return DecorateReportStore?.Invoke(store) ?? store;
            });
            if (QrGenerator is not null) services.AddSingleton(QrGenerator);
            if (BillingInterceptor is not null)
                services.ConfigureDbContext<EducationKeeptrack.Billing.Infrastructure.BillingDatabase>(options => options.AddInterceptors(BillingInterceptor));
            if (ReportingInterceptor is not null)
                services.ConfigureDbContext<EducationKeeptrack.Reporting.Infrastructure.ReportingDatabase>(options => options.AddInterceptors(ReportingInterceptor));
            if (ClassroomChangeHandler is not null)
            {
                services.AddSingleton(ClassroomChangeHandler);
            }
            if (DecorateBillingHandler is not null)
            {
                var subscription = services.Single(x => x.ServiceType == typeof(IClassroomChangeHandler)
                    && x.ImplementationType == typeof(EducationKeeptrack.Billing.Application.BillingClassroomHandler));
                services.Remove(subscription);
                services.AddScoped<EducationKeeptrack.Billing.Application.BillingClassroomHandler>();
                services.AddScoped<IClassroomChangeHandler>(provider => DecorateBillingHandler(
                    provider.GetRequiredService<EducationKeeptrack.Billing.Application.BillingClassroomHandler>()));
            }
            if (TuitionChangeHandler is not null) services.AddSingleton(TuitionChangeHandler);
            if (LearningResultChangeHandler is not null) services.AddSingleton(LearningResultChangeHandler);
        });
    }

    // Creates the empty test database without migrating it.
    public async Task CreateDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
        if (!reuseDatabase) await command.ExecuteNonQueryAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDatabase>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<EducationKeeptrack.Classroom.Infrastructure.ClassroomDatabase>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<EducationKeeptrack.ServiceAccess.Infrastructure.ServiceAccessDatabase>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<EducationKeeptrack.Billing.Infrastructure.BillingDatabase>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<EducationKeeptrack.Learning.Infrastructure.LearningDatabase>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<EducationKeeptrack.Reporting.Infrastructure.ReportingDatabase>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (!ownsDatabase) return;
        try { await reportStorage.DisposeAsync(); }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(adminConnection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<string> AddTeacher(string email, bool approved = true, bool confirmed = true)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var account = new Account { UserName = email, Email = email, EmailConfirmed = confirmed };
        var result = await users.CreateAsync(account, "Test-only!Password123");
        Assert.True(result.Succeeded, string.Join(",", result.Errors.Select(e => e.Description)));
        if (approved)
            await EducationKeeptrack.ServiceAccess.Infrastructure.DevelopmentProvisioning.GrantAsync(
                scope.ServiceProvider.GetRequiredService<EducationKeeptrack.ServiceAccess.Infrastructure.ServiceAccessDatabase>(),
                account.Id, Clock.GetUtcNow(), Clock.GetUtcNow().AddDays(30));
        return account.Id;
    }

    public const string Password = "Test-only!Password123";

    public async Task<string> AddAdmin(string email, bool enrollTotp = true)
    {
        using var scope = Services.CreateScope();
        if (enrollTotp) return (await AdminSetup.RunAsync(scope.ServiceProvider, email, Password)).AuthenticatorKey;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync("Admin")) await roles.CreateAsync(new IdentityRole("Admin"));
        var account = new Account { UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(account, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(account, "Admin")).Succeeded);
        return "";
    }

    public async Task LoginAdmin(HttpClient client, string email, string key)
    {
        var response = await Post(client, "/api/auth/login", new { email, password = Password, code = Totp.Code(key) });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> Put(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    public static async Task Login(HttpClient client, string email)
    {
        var response = await Post(client, "/api/auth/login", new { email, password = "Test-only!Password123" });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
