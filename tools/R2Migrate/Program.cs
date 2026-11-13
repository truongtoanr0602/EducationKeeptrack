using System.Text.Json;

namespace R2Migrate;

/// <summary>
/// Moves exported PDFs from the old store (SeaweedFS) to Cloudflare R2. Exit codes: 0 ok, 1 invalid usage/configuration or failure,
/// 2 the check ran but found problems that block the step. Configuration comes from the environment only; secrets are never printed.
/// <code>
/// R2M_DATABASE   PostgreSQL connection string (read-only use of the reporting schema)
/// SRC_ENDPOINT SRC_BUCKET SRC_ACCESS_KEY SRC_SECRET_KEY [SRC_REGION=us-east-1] [SRC_KEY_PREFIX] [SRC_ALLOW_HTTP=true]
/// DST_ENDPOINT DST_BUCKET DST_ACCESS_KEY DST_SECRET_KEY [DST_REGION=auto] [DST_KEY_PREFIX]
/// </code>
/// Commands: inventory [--store src|dst] [--against src|dst] [--deep] | copy [--reverse] --writes-paused | verify [--require-both]
/// | backup --dir D [--store src|dst] | restore --dir D [--to src|dst]. Every command accepts --report FILE. Only the stores a command
/// needs have to be configured.
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static async Task<int> Main(string[] args)
    {
        var stores = new List<IDisposable>();
        try
        {
            if (args.Length == 0) throw new ToolException("A command is required.");
            var flags = Flags.Parse(args[1..]);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            S3BlobStore? src = null, dst = null;
            S3BlobStore Source() => src ??= Track(Store("SRC", "us-east-1", "source"));
            S3BlobStore Destination() => dst ??= Track(Store("DST", "auto", "destination"));
            S3BlobStore Track(S3BlobStore store) { stores.Add(store); return store; }
            S3BlobStore Pick(string? name) => name switch { "dst" => Destination(), null or "src" => Source(), _ => throw new ToolException("Store must be src or dst.") };
            var metadata = new ReportingMetadata(Env("R2M_DATABASE"));
            object report; bool ok;
            switch (args[0])
            {
                case "inventory":
                    var inventory = await Migrator.InventoryAsync(Pick(flags.Value("store")), metadata, flags.Has("deep"), cts.Token,
                        flags.Value("against") is { } against ? Pick(against) : null);
                    (report, ok) = (inventory, inventory.Clean); break;
                case "copy":
                    var reverse = flags.Has("reverse");
                    var copy = await Migrator.CopyAsync(reverse ? Destination() : Source(), reverse ? Source() : Destination(), metadata, flags.Has("writes-paused"), TimeProvider.System, cts.Token);
                    (report, ok) = (copy, copy.Success); break;
                case "verify":
                    var verify = await Migrator.VerifyAsync(Source(), Destination(), metadata, cts.Token);
                    (report, ok) = (verify, flags.Has("require-both") ? verify.RollbackReady : verify.DestinationComplete); break;
                case "backup":
                    var manifest = await Migrator.BackupAsync(Pick(flags.Value("store")), metadata, flags.Required("dir"), TimeProvider.System, cts.Token);
                    (report, ok) = (new { manifest.CreatedAt, manifest.Source, Entries = manifest.Entries.Count }, true); break;
                case "restore":
                    var restore = await Migrator.RestoreAsync(flags.Required("dir"), Pick(flags.Value("to") ?? "dst"), cts.Token);
                    (report, ok) = (restore, restore.Success); break;
                default: throw new ToolException("Unknown command.");
            }
            var text = JsonSerializer.Serialize(report, Json);
            if (flags.Value("report") is { } file) await File.WriteAllTextAsync(file, text);
            Console.WriteLine(text);
            return ok ? 0 : 2;
        }
        catch (ToolException e)
        {
            Console.Error.WriteLine("R2Migrate: " + e.Message); // our own messages only; they never contain configuration values
            return 1;
        }
        catch (Exception e)
        {
            // SDK, EF and driver exceptions can carry endpoints, bucket names or connection strings.
            Console.Error.WriteLine($"R2Migrate failed ({e.GetType().Name}); check configuration and connectivity.");
            return 1;
        }
        finally { foreach (var store in stores) store.Dispose(); }
    }

    private static string Env(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new ToolException($"{name} is not set.");

    private static S3BlobStore Store(string prefix, string defaultRegion, string name) => new(name, new StoreSettings
    {
        Endpoint = Env($"{prefix}_ENDPOINT"), Bucket = Env($"{prefix}_BUCKET"), AccessKey = Env($"{prefix}_ACCESS_KEY"), SecretKey = Env($"{prefix}_SECRET_KEY"),
        Region = Environment.GetEnvironmentVariable($"{prefix}_REGION") is { Length: > 0 } r ? r : defaultRegion,
        KeyPrefix = Environment.GetEnvironmentVariable($"{prefix}_KEY_PREFIX") ?? "",
        AllowHttp = prefix == "SRC" && Environment.GetEnvironmentVariable("SRC_ALLOW_HTTP") == "true"
    });

    private sealed class Flags(Dictionary<string, string?> values)
    {
        public static Flags Parse(string[] args)
        {
            var values = new Dictionary<string, string?>();
            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new ToolException("Unexpected argument.");
                var name = args[i][2..];
                values[name] = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : null;
            }
            return new(values);
        }
        public bool Has(string name) => values.ContainsKey(name);
        public string? Value(string name) => values.GetValueOrDefault(name);
        public string Required(string name) => Value(name) ?? throw new ToolException($"--{name} is required.");
    }
}
