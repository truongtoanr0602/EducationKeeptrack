using EducationKeeptrack.Reporting.Application;
using EducationKeeptrack.Reporting.Infrastructure;

namespace ApiTests;

public sealed class ReportObjectPrefixTests
{
    [Theory]
    [InlineData("", "reports/a.pdf", "reports/a.pdf")]
    [InlineData("ekt-ci/abc/browser/", "reports/a.pdf", "ekt-ci/abc/browser/reports/a.pdf")]
    public void Physical_key_preserves_default_and_scopes_prefixed_objects(string prefix, string key, string expected)
        => Assert.Equal(expected, ReportObjectPrefix.Apply(prefix, key));

    [Theory]
    [InlineData("/root/", "reports/a.pdf")]
    [InlineData("root", "reports/a.pdf")]
    [InlineData("root/../", "reports/a.pdf")]
    [InlineData("root\\", "reports/a.pdf")]
    [InlineData("root/", "../outside.pdf")]
    [InlineData("root/", "/outside.pdf")]
    [InlineData("root/", "a//b.pdf")]
    [InlineData("root/", "")]
    public void Unsafe_paths_are_rejected(string prefix, string key)
        => Assert.Throws<ReportStorageException>(() => ReportObjectPrefix.Apply(prefix, key));
}
