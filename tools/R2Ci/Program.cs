using System.Text.Json;
namespace R2Ci;

internal static class CiProgram
{
public static async Task<int> Main(string[] args)
{

try
{
    if (args is ["browser"]) return Microsoft.Playwright.Program.Main(["install", "--with-deps", "chromium"]);
    if (args.Length < 2) throw new InvalidOperationException("Invalid CI command.");
    if (args[0] == "init" && args.Length == 2)
    {
        _ = RunStorage.Configuration();
        var state = RunState.Create();
        using var file = new FileStream(args[1], FileMode.CreateNew);
        JsonSerializer.Serialize(file, state);
    }
    else
    {
        var state = JsonSerializer.Deserialize<RunState>(File.ReadAllText(args[1]))
            ?? throw new InvalidOperationException("Missing CI run state.");
        state.Validate();
        if (args[0] == "prefix" && args.Length == 2) Console.WriteLine(state.Prefix);
        else if (args[0] == "cleanup" && args.Length == 3)
        {
            File.WriteAllText(args[2], JsonSerializer.Serialize(new CleanupSummary(state.Id, false, 0)));
            using var storage = new RunStorage(RunStorage.Configuration());
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var summary = await storage.CleanupAsync(state, timeout.Token);
            File.WriteAllText(args[2], JsonSerializer.Serialize(summary));
        }
        else if (args[0] == "evidence" && args.Length == 6)
        {
            try
            {
                var evidence = Evidence.Read(state, args[2], args[3], args[4]);
                File.WriteAllText(args[5], JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                File.WriteAllText(args[5], JsonSerializer.Serialize(new RunEvidence(state.Id, "Failed", 0, 0, 0)));
                throw;
            }
        }
        else throw new InvalidOperationException("Invalid CI command.");
    }
    return 0;
}
catch
{
    // SDK exceptions and raw reports can contain sensitive URLs or credentials.
    Console.Error.WriteLine("R2 CI command failed; configuration, owned prefix, cleanup and passing evidence are required.");
    return 1;
}

}
}
