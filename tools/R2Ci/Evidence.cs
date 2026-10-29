using System.Text.Json;
using System.Xml.Linq;

namespace R2Ci;

public sealed record RunEvidence(Guid RunId, string Status, int ApiPassed, int BrowserPassed, int ObjectsDeleted);

public static class Evidence
{
    private static readonly string[] RequiredApi = [
        "Download_requires_current_owner_session_and_unexpired_bound_token",
        "Expired_teacher_exports_saved_content_and_block_or_revoke_prevents_download(block: True)",
        "Expired_teacher_exports_saved_content_and_block_or_revoke_prevents_download(block: False)",
        "Network_or_credential_failure_keeps_export_failed_and_valid_storage_can_retry(invalidCredentials: True)",
        "Network_or_credential_failure_keeps_export_failed_and_valid_storage_can_retry(invalidCredentials: False)",
        "Export_creates_private_pdf_with_vietnamese_confirmed_results_finances_and_fallback",
        "R2_prefix_cleanup_preserves_other_runs_and_prefixed_downloads_match"
    ];
    private static readonly string[] RequiredBrowser = [
        "teacher edits monthly draft, previews and downloads only selected student",
        "uncertain export retries same command and source conflict preserves draft"
    ];

    public static RunEvidence Read(RunState state, string trxPath, string browserPath, string cleanupPath)
    {
        state.Validate();
        foreach (var path in new[] { trxPath, browserPath, cleanupPath })
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < state.CreatedAt.UtcDateTime)
                throw new InvalidOperationException("Missing or stale CI evidence.");
        var cleanup = JsonSerializer.Deserialize<CleanupSummary>(File.ReadAllText(cleanupPath));
        if (cleanup is null || !cleanup.Success || cleanup.RunId != state.Id || cleanup.Deleted < 0)
            throw new InvalidOperationException("Run cleanup did not succeed.");
        var document = XDocument.Load(trxPath);
        var api = document.Descendants().Where(x => x.Name.LocalName == "UnitTestResult").ToArray();
        var summary = document.Descendants().SingleOrDefault(x => x.Name.LocalName == "ResultSummary");
        var counters = summary?.Elements().SingleOrDefault(x => x.Name.LocalName == "Counters");
        if ((string?)summary?.Attribute("outcome") != "Completed" || counters is null ||
            new[] { "total", "executed", "passed" }.Any(name => (int?)counters.Attribute(name) != api.Length))
            throw new InvalidOperationException("API run did not complete all reported cases.");
        if (api.Length == 0 || api.Any(x => (string?)x.Attribute("outcome") != "Passed") ||
            RequiredApi.Any(name => !api.Any(x => ((string?)x.Attribute("testName"))?.Contains(name, StringComparison.Ordinal) == true)))
            throw new InvalidOperationException("API evidence is incomplete, failed, or skipped.");
        using var browser = JsonDocument.Parse(File.ReadAllText(browserPath));
        if (browser.RootElement.GetProperty("errors").GetArrayLength() != 0)
            throw new InvalidOperationException("Browser run contains errors.");
        var cases = new List<(string Title, string Project)>();
        void Visit(JsonElement suite)
        {
            if (suite.TryGetProperty("specs", out var specs)) foreach (var spec in specs.EnumerateArray())
            foreach (var test in spec.GetProperty("tests").EnumerateArray())
            {
                var results = test.GetProperty("results");
                if (test.GetProperty("expectedStatus").GetString() != "passed" ||
                    test.GetProperty("status").GetString() != "expected" || results.GetArrayLength() == 0 ||
                    results.EnumerateArray().Any(x => x.GetProperty("status").GetString() != "passed"))
                    throw new InvalidOperationException("Browser evidence contains failed, retried, or skipped cases.");
                cases.Add((spec.GetProperty("title").GetString()!, test.GetProperty("projectName").GetString()!));
            }
            if (suite.TryGetProperty("suites", out var children)) foreach (var child in children.EnumerateArray()) Visit(child);
        }
        Visit(browser.RootElement);
        if (RequiredBrowser.Any(name => new[] { "desktop", "mobile" }.Any(project => !cases.Contains((name, project)))))
            throw new InvalidOperationException("Missing required PDF browser cases.");
        // Only aggregate counts leave this boundary. Never copy raw test output, names, URLs or errors.
        return new(state.Id, "Passed", api.Length, cases.Count, cleanup.Deleted);
    }
}
