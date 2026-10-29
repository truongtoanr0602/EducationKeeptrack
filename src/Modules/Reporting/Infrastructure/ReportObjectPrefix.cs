using EducationKeeptrack.Reporting.Application;

namespace EducationKeeptrack.Reporting.Infrastructure;

public static class ReportObjectPrefix
{
    public static string Apply(string prefix, string key)
    {
        static bool Safe(string path) => path.Length > 0 && path.Split('/').All(segment =>
            segment.Length > 0 && segment is not "." and not ".." &&
            segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'));
        if ((prefix.Length > 0 && (!prefix.EndsWith('/') || !Safe(prefix[..^1]))) || !Safe(key))
            throw new ReportStorageException("Đường dẫn kho PDF không hợp lệ.");
        return prefix + key;
    }
}
