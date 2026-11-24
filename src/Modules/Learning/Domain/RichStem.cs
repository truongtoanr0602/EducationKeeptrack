using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;
namespace EducationKeeptrack.Learning.Domain;

/// <summary>
/// The rich stem of a typed fill-in (ADR 0008): a small, fixed set of tags and classes. Nothing else survives, and no URL
/// does either: an image is the name of an uploaded file (data-file), turned into a signed link only when the stem is served.
/// </summary>
public static class RichStem
{
    public const int MaxLength = 20_000;
    /// <summary>Longer input is not cleaned (the check rejects it first), so a huge paste cannot cost the server a parse.</summary>
    public const int MaxCleaned = 100_000;

    public static readonly string[] Tags = ["p", "br", "strong", "em", "u", "s", "ul", "ol", "li", "span", "h2", "h3", "h4", "img"];
    /// <summary>Four fonts, four sizes, six colours and three alignments, named by class; the blank marker is a span with data-blank.</summary>
    public static readonly string[] Classes =
    [
        "ff-sans", "ff-serif", "ff-hand", "ff-mono", "fs-s", "fs-m", "fs-l", "fs-xl",
        "fc-1", "fc-2", "fc-3", "fc-4", "fc-5", "fc-6", "ta-left", "ta-center", "ta-right", "blank"
    ];

    private static readonly Regex Number = new(@"^\d{1,2}$", RegexOptions.Compiled);
    private static readonly Regex FileAttribute = new(@"data-file=""([^""]*)""", RegexOptions.Compiled);
    private static readonly Regex ImageTag = new(@"<img\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static HtmlSanitizer Build()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in Tags) sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in new[] { "class", "data-file", "data-blank", "alt" }) sanitizer.AllowedAttributes.Add(attribute);
        sanitizer.AllowedClasses.Clear();
        foreach (var name in Classes) sanitizer.AllowedClasses.Add(name);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowDataAttributes = false;
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not AngleSharp.Dom.IElement element) return;
            if (element.HasAttribute("data-blank") && !Number.IsMatch(element.GetAttribute("data-blank") ?? "")) element.RemoveAttribute("data-blank");
            if (element.GetAttribute("data-file") is { } file && (file.Length is 0 or > 200 || file.Contains('/') || file.Contains('\\')))
                element.RemoveAttribute("data-file");
            if (element.LocalName == "img" && !element.HasAttribute("data-file")) element.Remove();
        };
        return sanitizer;
    }

    /// <summary>The stem reduced to the allowed tags, classes and attributes.</summary>
    public static string Clean(string? html) => string.IsNullOrWhiteSpace(html) ? "" : Build().Sanitize(html).Trim();

    /// <summary>Plain text of a cleaned stem: blocks and line breaks become new lines, images disappear, blank markers stay as {{n}}.</summary>
    public static string ToText(string cleanHtml)
    {
        var text = Regex.Replace(cleanHtml, @"<br\s*/?>|</p>|</li>|</h[2-4]>", "\n", RegexOptions.IgnoreCase);
        text = WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", ""));
        return Regex.Replace(Regex.Replace(text, @"[ \t]+", " "), @"\s*\n\s*", "\n").Trim();
    }

    /// <summary>The uploaded files a cleaned stem refers to, by name.</summary>
    public static IReadOnlyList<string> Files(string cleanHtml) =>
        FileAttribute.Matches(cleanHtml).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Adds a signed link to every image of a cleaned stem; an image whose file is gone is dropped.</summary>
    public static string Resolve(string? cleanHtml, Func<string, string?> url) =>
        string.IsNullOrEmpty(cleanHtml) ? "" : ImageTag.Replace(cleanHtml, match =>
        {
            var file = FileAttribute.Match(match.Value);
            var link = file.Success ? url(WebUtility.HtmlDecode(file.Groups[1].Value)) : null;
            return link is null ? "" : match.Value.Replace("<img", $"<img src=\"{WebUtility.HtmlEncode(link)}\"", StringComparison.OrdinalIgnoreCase);
        });
}
