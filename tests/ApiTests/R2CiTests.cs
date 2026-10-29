using System.Text.Json;
using System.Xml.Linq;
using R2Ci;

namespace ApiTests;

public sealed class R2CiTests
{
    [Theory]
    [InlineData("ekt-ci/")]
    [InlineData("")]
    [InlineData("ekt-ci/../")]
    [InlineData("ekt-ci/00000000000000000000000000000000/")]
    public void Cleanup_refuses_prefix_not_owned_by_run(string prefix)
        => Assert.Throws<InvalidOperationException>(() => new RunState(Guid.Parse("11111111-1111-1111-1111-111111111111"), prefix).Validate());

    [Fact]
    public void Evidence_requires_every_security_and_storage_case_on_both_viewports()
    {
        using var files = new EvidenceFiles();
        var evidence = Evidence.Read(files.State, files.Trx, files.Browser, files.Cleanup);
        var json = JsonSerializer.Serialize(evidence);
        Assert.Contains("Passed", json);
        Assert.DoesNotContain("SECRET", json);
        Assert.DoesNotContain("token=", json);
        files.ApiNames.RemoveAt(0); files.Write();
        Assert.Throws<InvalidOperationException>(() => Evidence.Read(files.State, files.Trx, files.Browser, files.Cleanup));
    }

    [Theory]
    [InlineData("skipped")]
    [InlineData("failed")]
    [InlineData("cleanup")]
    [InlineData("missing")]
    [InlineData("stale")]
    [InlineData("browser-skipped")]
    [InlineData("browser-missing-mobile")]
    [InlineData("api-aborted")]
    [InlineData("missing-revocation")]
    [InlineData("missing-blocked")]
    public void Evidence_rejects_incomplete_or_unsuccessful_runs(string failure)
    {
        using var files = new EvidenceFiles();
        if (failure == "skipped") files.Outcome = "NotExecuted";
        if (failure == "failed") files.Outcome = "Failed";
        if (failure == "browser-skipped") files.BrowserStatus = "skipped";
        if (failure == "browser-missing-mobile") files.Mobile = false;
        if (failure == "api-aborted") files.Completed = false;
        if (failure == "missing-revocation") files.ApiNames.RemoveAll(name => name.Contains("block: False"));
        if (failure == "missing-blocked") files.ApiNames.RemoveAll(name => name.Contains("block: True"));
        if (failure == "cleanup") files.Cleaned = false;
        if (failure == "stale") files.CreatedAt = DateTimeOffset.UtcNow.AddDays(1);
        files.Write();
        if (failure == "missing") File.Delete(files.Browser);
        Assert.ThrowsAny<Exception>(() => Evidence.Read(files.State, files.Trx, files.Browser, files.Cleanup));
    }

    private sealed class EvidenceFiles : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "ekt-evidence-" + Guid.NewGuid().ToString("N"));
        public RunState State { get; private set; } = RunState.Create();
        public string Trx => Path.Combine(directory, "api.trx");
        public string Browser => Path.Combine(directory, "browser.json");
        public string Cleanup => Path.Combine(directory, "cleanup.json");
        public string Outcome = "Passed", BrowserStatus = "passed";
        public bool Cleaned = true, Mobile = true, Completed = true;
        public DateTimeOffset CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        public List<string> ApiNames = [
            "Download_requires_current_owner_session_and_unexpired_bound_token",
            "Expired_teacher_exports_saved_content_and_block_or_revoke_prevents_download(block: True)",
            "Expired_teacher_exports_saved_content_and_block_or_revoke_prevents_download(block: False)",
            "Network_or_credential_failure_keeps_export_failed_and_valid_storage_can_retry(invalidCredentials: True)",
            "Network_or_credential_failure_keeps_export_failed_and_valid_storage_can_retry(invalidCredentials: False)",
            "Export_creates_private_pdf_with_vietnamese_confirmed_results_finances_and_fallback",
            "R2_prefix_cleanup_preserves_other_runs_and_prefixed_downloads_match"
        ];
        public EvidenceFiles() { Directory.CreateDirectory(directory); Write(); }
        public void Write()
        {
            State = State with { CreatedAt = CreatedAt };
            new XDocument(new XElement("TestRun", new XElement("ResultSummary", new XAttribute("outcome", Completed ? "Completed" : "Aborted"),
                new XElement("Counters", new XAttribute("total", ApiNames.Count), new XAttribute("executed", ApiNames.Count), new XAttribute("passed", ApiNames.Count))),
                new XElement("Results", ApiNames.Select(name =>
                new XElement("UnitTestResult", new XAttribute("testName", name), new XAttribute("outcome", Outcome),
                    new XElement("Output", "SECRET https://example.test/?token=SECRET")))))).Save(Trx);
            var projects = Mobile ? new[] { "desktop", "mobile" } : new[] { "desktop" };
            File.WriteAllText(Browser, JsonSerializer.Serialize(new {
                errors = Array.Empty<object>(), suites = new[] { new { specs = new[] {
                    new { title = "teacher edits monthly draft, previews and downloads only selected student", tests = projects.Select(project => new {
                        projectName = project, expectedStatus = "passed", status = "expected", results = new[] { new { status = BrowserStatus } }
                    }) },
                    new { title = "uncertain export retries same command and source conflict preserves draft", tests = projects.Select(project => new {
                        projectName = project, expectedStatus = "passed", status = "expected", results = new[] { new { status = BrowserStatus } }
                    }) }
                } } }
            }));
            File.WriteAllText(Cleanup, JsonSerializer.Serialize(new CleanupSummary(State.Id, Cleaned, 3)));
        }
        public void Dispose() => Directory.Delete(directory, true);
    }
}
