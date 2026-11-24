using System.Net;
using System.Net.Http.Headers;
using EducationKeeptrack.Learning.Domain;
using static ApiTests.LearningHttp;
namespace ApiTests;

/// <summary>The rich stem of a typed fill-in (ADR 0008, #114): what the server lets through, and uploaded audio.</summary>
public sealed class RichStemTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Theory]
    [InlineData("<p>Xin chào<script>alert(1)</script></p>")]
    [InlineData("<p onclick=\"alert(1)\">Xin chào</p>")]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\" data-file=\"a.png\">")]
    [InlineData("<p style=\"background:url(javascript:alert(1))\">x</p>")]
    [InlineData("<a href=\"javascript:alert(1)\">bấm</a>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe><p>x</p>")]
    [InlineData("<object data=\"x\"></object><embed src=\"x\"><p>x</p>")]
    [InlineData("<svg onload=\"alert(1)\"></svg><p>x</p>")]
    [InlineData("<p class=\"ff-serif\" id=\"x\" title=\"t\">x</p>")]
    public void Nothing_that_runs_or_loads_from_elsewhere_survives(string dirty)
    {
        var clean = RichStem.Clean(dirty).ToLowerInvariant();
        foreach (var bad in new[] { "script", "onclick", "onerror", "onload", "javascript", "<a", "iframe", "object", "embed", "svg", "style=", "id=\"", "title=", "evil.example", "src=" })
            Assert.DoesNotContain(bad, clean);
    }

    [Fact]
    public void The_formatting_the_editor_makes_is_kept()
    {
        const string html = "<h2 class=\"ta-center\">Bài 1</h2><p class=\"ta-left\"><strong>đậm</strong> <em>nghiêng</em> <u>gạch chân</u> <s>gạch ngang</s> "
            + "<span class=\"ff-serif fs-l fc-3\">chữ có chân</span> điền <span class=\"blank\" data-blank=\"1\">{{1}}</span></p><ul><li>a</li></ul><ol><li>b</li></ol>"
            + "<p><img data-file=\"dongho.png\" alt=\"đồng hồ\"></p>";
        var clean = RichStem.Clean(html);
        foreach (var kept in new[] { "<h2 class=\"ta-center\">", "<strong>", "<em>", "<u>", "<s>", "class=\"ff-serif fs-l fc-3\"", "data-blank=\"1\"", "{{1}}", "<ul>", "<ol>", "data-file=\"dongho.png\"", "alt=" })
            Assert.Contains(kept, clean);
    }

    [Fact]
    public void Classes_outside_the_fixed_set_and_images_without_a_file_are_dropped()
    {
        var clean = RichStem.Clean("<p class=\"ff-serif evil-class\">x</p><img data-file=\"\"><img><img data-file=\"../etc/passwd\"><span data-blank=\"abc\">y</span>");
        Assert.DoesNotContain("evil-class", clean);
        Assert.Contains("ff-serif", clean);
        Assert.DoesNotContain("<img", clean);
        Assert.DoesNotContain("data-blank", clean);
        Assert.Contains("y", clean);
    }

    [Fact]
    public void The_plain_text_of_a_stem_keeps_the_blank_markers_and_line_breaks()
    {
        var clean = RichStem.Clean("<p>Thủ đô của <strong>Việt Nam</strong> là <span class=\"blank\" data-blank=\"1\">{{1}}</span>.</p><p>Dòng hai<br>dòng ba</p><p><img data-file=\"a.png\"></p>");
        Assert.Equal("Thủ đô của Việt Nam là {{1}}.\nDòng hai\ndòng ba", RichStem.ToText(clean));
        Assert.Equal(["a.png"], RichStem.Files(clean));
        Assert.True(Question.TokensInOrder(RichStem.ToText(clean)));
    }

    [Fact]
    public void An_image_gets_its_signed_link_only_when_the_stem_is_served_and_a_missing_file_drops_the_image()
    {
        var clean = RichStem.Clean("<p>a</p><img data-file=\"co.png\"><img data-file=\"mat.png\">");
        var served = RichStem.Resolve(clean, name => name == "co.png" ? "/api/learning/question-images/abc%2B" : null);
        Assert.Contains("src=\"/api/learning/question-images/abc%2B\"", served);
        Assert.DoesNotContain("mat.png", served);
        Assert.Equal("", RichStem.Resolve(null, _ => "x"));
    }

    private static readonly Guid Skill = Guid.NewGuid();
    private static QuestionInput Typed(string? html, string? audio = null, string type = QuestionType.FillBlanks, string? mode = QuestionType.FillType) =>
        new("R1", type, QuestionLevel.Recall, Skill, [], null, "Thủ đô là {{1}}", null, [], [["Hà Nội"]], false, 0, null, mode, false, null, html, audio);

    [Fact]
    public void A_rich_stem_and_an_audio_file_belong_to_a_typed_fill_in_and_must_refer_to_uploaded_files()
    {
        var live = new HashSet<Guid> { Skill };
        var none = new HashSet<string>();
        Assert.Empty(Question.Validate(Typed("<p>Thủ đô là {{1}}</p>"), live, none, none));
        Assert.Contains("stemHtml", Question.Validate(Typed("<p>x</p>", mode: QuestionType.FillChips) with { Options = [new("1", "a", null), new("2", "b", null)], Answer = [["a"]], Stem = "x [ ]" }, live, none, none).Keys);
        Assert.Contains("stemHtml", Question.Validate(Typed("<img data-file=\"a.png\">{{1}}"), live, none, none).Keys);
        Assert.Empty(Question.Validate(Typed("<img data-file=\"a.png\">{{1}}"), live, new HashSet<string> { QuestionImage.NormalizeName("a.png") }, none));
        Assert.Contains("stemHtml", Question.Validate(Typed(new string('x', RichStem.MaxLength + 1)), live, none, none).Keys);
        Assert.Contains("audioFile", Question.Validate(Typed(null, "bai.mp3"), live, none, none).Keys);
        Assert.Empty(Question.Validate(Typed(null, "bai.mp3"), live, none, new HashSet<string> { QuestionImage.NormalizeName("bai.mp3") }));
        Assert.Contains("audioFile", Question.Validate(Typed(null, "bai.mp3", QuestionType.Essay, null) with { Options = [], Answer = [] }, live, none, new HashSet<string> { QuestionImage.NormalizeName("bai.mp3") }).Keys);
    }

    // ---- API ----

    private static byte[] Mp3(int length = 4096)
    {
        var bytes = new byte[length];
        "ID3"u8.CopyTo(bytes);
        bytes[3] = 3;
        for (var i = 10; i < length; i++) bytes[i] = (byte)(i % 251);
        return bytes;
    }

    private static byte[] M4a()
    {
        var bytes = new byte[4096];
        "ftypM4A "u8.CopyTo(bytes.AsSpan(4));
        return bytes;
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, string classId, byte[] bytes, string name, string type)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(file, "file", name);
        return await TestApplication.PostForm(client, $"/api/learning/classes/{classId}/question-images", form);
    }

    [Fact]
    public async Task Audio_is_accepted_only_as_a_real_mp3_or_m4a_of_five_megabytes_or_less_and_is_served_in_byte_ranges()
    {
        var (client, classId, _) = await LearningHttp.Teacher(app, "audio");
        var ok = await Json(await Upload(client, classId, Mp3(), "nghe1.mp3", "audio/mpeg"), HttpStatusCode.Created);
        Assert.Equal("audio/mpeg", ok.GetProperty("contentType").GetString());
        var m4a = await Json(await Upload(client, classId, M4a(), "nghe2.m4a", "audio/mp4"), HttpStatusCode.Created);
        Assert.Equal("audio/mp4", m4a.GetProperty("contentType").GetString());
        // Declared type is never trusted: text posing as mp3, and an exe posing as audio.
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(client, classId, "đây là chữ không phải âm thanh, dài đủ để qua kiểm tra độ dài tối thiểu"u8.ToArray(), "gia.mp3", "audio/mpeg")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(client, classId, Mp3(), "x.wav", "audio/wav")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(client, classId, Mp3(5 * 1024 * 1024 + 1), "dai.mp3", "audio/mpeg")).StatusCode);

        var url = ok.GetProperty("url").GetString()!;
        var full = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        Assert.Equal("audio/mpeg", full.Content.Headers.ContentType!.MediaType);
        Assert.Equal(4096, (await full.Content.ReadAsByteArrayAsync()).Length);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(100, 199);
        var part = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, part.StatusCode);
        Assert.Equal(100, (await part.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task A_typed_fill_in_stores_the_cleaned_stem_and_derives_the_plain_text_from_it()
    {
        var (client, classId, _) = await LearningHttp.Teacher(app, "rich");
        using var form = new MultipartFormDataContent();
        var template = await client.GetAsync($"/api/learning/classes/{classId}/import-template");
        var file = new ByteArrayContent(await template.Content.ReadAsByteArrayAsync());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "mau.xlsx");
        await Json(await TestApplication.PostForm(client, $"/api/learning/classes/{classId}/imports?commit=true", form), HttpStatusCode.OK);
        var skill = (await Json(await client.GetAsync($"/api/learning/classes/{classId}/questions"), HttpStatusCode.OK)).GetProperty("skills")[0].GetProperty("id").GetString();
        await Json(await Upload(client, classId, Mp3(), "nghe.mp3", "audio/mpeg"), HttpStatusCode.Created);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        await Json(await Upload(client, classId, png, "dongho.png", "image/png"), HttpStatusCode.Created);

        var created = await Json(await Post(client, $"/api/learning/classes/{classId}/questions", new
        {
            code = "RICH1", type = "fill_blanks", fillMode = "type", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(),
            stem = "bị bỏ qua", stemHtml = "<h2>Bài 1</h2><p onclick=\"x()\">Thủ đô là <span class=\"blank\" data-blank=\"1\">{{1}}</span><script>alert(1)</script></p><img data-file=\"dongho.png\" src=\"http://evil/x.png\">",
            audioFile = "nghe.mp3", options = Array.Empty<object>(), answer = new[] { new[] { "Hà Nội" } }, version = 0
        }), HttpStatusCode.Created);
        var html = created.GetProperty("stemHtml").GetString()!;
        Assert.DoesNotContain("script", html);
        Assert.DoesNotContain("onclick", html);
        Assert.DoesNotContain("evil", html);
        Assert.Contains("data-file=\"dongho.png\"", html);
        Assert.Equal("Bài 1\nThủ đô là {{1}}", created.GetProperty("stem").GetString());
        Assert.Equal("nghe.mp3", created.GetProperty("audioFile").GetString());
        Assert.Equal(1, created.GetProperty("blanks").GetInt32());

        // A missing file and a stem for the wrong kind are refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, $"/api/learning/classes/{classId}/questions", new
        {
            code = "RICH2", type = "fill_blanks", fillMode = "type", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(),
            stemHtml = "<p>{{1}}</p><img data-file=\"khong-co.png\">", options = Array.Empty<object>(), answer = new[] { new[] { "a" } }, version = 0
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, $"/api/learning/classes/{classId}/questions", new
        {
            code = "RICH3", type = "essay", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "x",
            stemHtml = "<p>x</p>", options = Array.Empty<object>(), answer = Array.Empty<string[]>(), version = 0
        })).StatusCode);
    }
}
