namespace ApiTests;

public sealed class StartupMigrationCheckTests
{
    [Fact]
    public async Task Host_refuses_to_start_on_a_database_with_pending_migrations()
    {
        await using var app = new TestApplication { CheckPendingMigrations = true };
        await app.CreateDatabaseAsync();
        var error = Assert.ThrowsAny<Exception>(() => app.Client());
        var message = error.ToString();
        Assert.Contains("--migrate", message);
        Assert.Contains("IdentityDatabase", message);
    }

    [Fact]
    public async Task Host_starts_once_every_module_is_migrated()
    {
        await using var original = new TestApplication(); await original.InitializeAsync();
        await original.StopHostAsync();
        await using var checkedHost = original.ReopenWithMigrationCheck();
        using var client = checkedHost.Client();
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
}
