using System.Text.Json;
using EducationKeeptrack.Learning.Domain;
namespace ApiTests;

/// <summary>Dạng câu hỏi và điểm (ADR 0008, #112): validation and scoring of the four kinds; no database needed.</summary>
public sealed class QuestionTypeRuleTests
{
    private static readonly Guid Skill = Guid.NewGuid();
    private static readonly HashSet<Guid> Live = [Skill];
    private static readonly HashSet<string> NoImages = [];
    private static readonly QuestionOption[] Four = [new("A", "2", null), new("B", "3", null), new("C", "4", null), new("D", "5", null)];

    private static QuestionInput Multi(string[] keys, decimal? points = null, QuestionOption[]? options = null) =>
        new("M1", QuestionType.MultiChoice, QuestionLevel.Recall, Skill, [], null, "Chọn các số chẵn", null, options ?? Four, [keys], false, 0, points);

    private static QuestionInput Typed(string stem, string[][] answer, bool ignoreDiacritics = false, decimal? points = null) =>
        new("T1", QuestionType.FillBlanks, QuestionLevel.Recall, Skill, [], null, stem, null, [], answer, false, 0, points, QuestionType.FillType, ignoreDiacritics);

    private static QuestionInput Essay(string? sample = null, decimal? points = null) =>
        new("E1", QuestionType.Essay, QuestionLevel.Apply, Skill, [], null, "Giải thích vì sao", null, [], [], false, 0, points, null, false, sample);

    private static QuestionSnapshot Snap(QuestionInput input) => new Question("owner", Guid.NewGuid(), input, DateTimeOffset.UnixEpoch).Snapshot();
    private static Dictionary<string, string[]> Check(QuestionInput input) => Question.Validate(input, Live, NoImages);

    [Fact]
    public void A_multiple_choice_question_needs_three_to_six_options_and_two_or_more_distinct_right_keys()
    {
        Assert.Empty(Check(Multi(["A", "C"])));
        Assert.Empty(Check(Multi(["a", "c"])));
        Assert.Contains("answer", Check(Multi(["A"])).Keys);
        Assert.Contains("answer", Check(Multi(["A", "A"])).Keys);
        Assert.Contains("answer", Check(Multi(["A", "Z"])).Keys);
        Assert.Contains("options", Check(Multi(["A", "B"], options: [new("A", "1", null), new("B", "2", null)])).Keys);
        Assert.Contains("options", Check(Multi(["A", "B"], options: [.. Four, new("E", "6", null), new("F", "7", null), new("G", "8", null)])).Keys);
        Assert.Empty(Check(Multi(["A", "F"], options: [.. Four, new("E", "6", null), new("F", "7", null)])));
    }

    [Fact]
    public void A_multiple_choice_question_scores_only_for_exactly_the_right_set()
    {
        var q = Snap(Multi(["A", "C"], points: 2m));
        Assert.Equal(2m, q.Score(["A", "C"]));
        Assert.Equal(2m, q.Score(["c", " a "]));
        Assert.Equal(0m, q.Score(["A"]));
        Assert.Equal(0m, q.Score(["A", "C", "D"]));
        Assert.Equal(0m, q.Score(["A", "B"]));
        Assert.Equal(0m, q.Score([]));
        Assert.Equal(0m, q.Score(null));
        Assert.True(q.IsCorrect(["A", "C"]));
        Assert.False(q.IsCorrect(["A"]));
    }

    [Fact]
    public void Points_are_between_a_quarter_and_ten_in_quarter_steps_and_default_to_one()
    {
        foreach (var ok in new[] { 0.25m, 1m, 1.5m, 9.75m, 10m }) Assert.Empty(Check(Multi(["A", "C"], ok)));
        foreach (var bad in new[] { 0m, 0.1m, 0.3m, 10.25m, -1m }) Assert.Contains("points", Check(Multi(["A", "C"], bad)).Keys);
        Assert.Equal(1m, Snap(Multi(["A", "C"])).Points);
    }

    [Fact]
    public void A_typed_fill_in_needs_numbered_blanks_in_order_and_an_answer_for_each()
    {
        Assert.Empty(Check(Typed("Thủ đô của {{1}} là {{2}}.", [["Việt Nam"], ["Hà Nội", "Ha Noi"]])));
        Assert.Contains("stem", Check(Typed("Không có chỗ trống", [["a"]])).Keys);
        Assert.Contains("stem", Check(Typed("{{2}} rồi {{1}}", [["a"], ["b"]])).Keys);
        Assert.Contains("stem", Check(Typed("{{1}} và {{1}}", [["a"]])).Keys);
        Assert.Contains("answer", Check(Typed("{{1}} và {{2}}", [["a"]])).Keys);
        Assert.Contains("answer", Check(Typed("{{1}}", [[" "]])).Keys);
        var eleven = string.Join(" ", Enumerable.Range(1, 11).Select(i => "{{" + i + "}}"));
        Assert.Contains("stem", Check(Typed(eleven, Enumerable.Range(1, 11).Select(_ => new[] { "x" }).ToArray())).Keys);
        var ten = string.Join(" ", Enumerable.Range(1, 10).Select(i => "{{" + i + "}}"));
        Assert.Empty(Check(Typed(ten, Enumerable.Range(1, 10).Select(_ => new[] { "x" }).ToArray())));
    }

    [Fact]
    public void A_typed_fill_in_ignores_case_and_spare_spaces_but_not_accents_unless_asked()
    {
        var q = Snap(Typed("{{1}}", [["Hà Nội", "Ha Noi"]]));
        Assert.True(q.IsCorrect(["hà nội"]));
        Assert.True(q.IsCorrect(["  HÀ   NỘI "]));
        Assert.True(q.IsCorrect(["ha noi"]), "an accepted alternative without accents");
        Assert.False(q.IsCorrect(["ha nội"]));
        Assert.False(q.IsCorrect(["Hải Phòng"]));
        Assert.False(q.IsCorrect([""]));

        var strict = Snap(Typed("{{1}}", [["Hà Nội"]]));
        Assert.False(strict.IsCorrect(["ha noi"]));
        var loose = Snap(Typed("{{1}}", [["Hà Nội"]], ignoreDiacritics: true));
        Assert.True(loose.IsCorrect(["ha noi"]));
        Assert.True(loose.IsCorrect(["HA NỘI"]));
        Assert.True(Snap(Typed("{{1}}", [["Đà Nẵng"]], ignoreDiacritics: true)).IsCorrect(["da nang"]));
    }

    [Fact]
    public void A_typed_fill_in_treats_a_decimal_comma_and_a_decimal_point_alike()
    {
        var q = Snap(Typed("{{1}}", [["0,5"]]));
        Assert.True(q.IsCorrect(["0.5"]));
        Assert.True(q.IsCorrect(["0,50"]));
        Assert.False(q.IsCorrect(["0.6"]));
        Assert.False(Snap(Typed("{{1}}", [["abc"]])).IsCorrect(["0.5"]));
    }

    [Fact]
    public void A_fill_in_with_several_blanks_shares_its_points_equally_and_each_right_blank_earns_its_part()
    {
        var q = Snap(Typed("{{1}} {{2}} {{3}} {{4}}", [["a"], ["b"], ["c"], ["d"]], points: 2m));
        Assert.Equal(2m, q.Score(["a", "b", "c", "d"]));
        Assert.Equal(1.5m, q.Score(["a", "b", "c", "x"]));
        Assert.Equal(0.5m, q.Score(["a", "x", "", "y"]));
        Assert.Equal(0m, q.Score(["x", "x", "x", "x"]));
        Assert.True(q.Score(["a", "b"]) == 0m, "a response with the wrong number of blanks earns nothing");
        Assert.False(q.IsCorrect(["a", "b", "c", "x"]));
        // 1 point over 3 blanks: 0.33 + 0.33 + 0.33 must not exceed the point, so each prefix is rounded from the exact share.
        var thirds = Snap(Typed("{{1}} {{2}} {{3}}", [["a"], ["b"], ["c"]]));
        Assert.Equal(0.33m, thirds.Score(["a", "x", "x"]));
        Assert.Equal(0.67m, thirds.Score(["a", "b", "x"]));
        Assert.Equal(1m, thirds.Score(["a", "b", "c"]));
    }

    [Fact]
    public void The_original_tap_a_button_fill_in_still_works_and_counts_each_blank()
    {
        var input = new QuestionInput("C1", QuestionType.FillBlanks, QuestionLevel.Recall, Skill, [], null, "6 chục [ ] đơn vị [ ]", null,
            [new("1", "4", null), new("2", "6", null), new("3", "10", null)], [["6"], ["4"]], false, 0);
        Assert.Empty(Check(input));
        var q = Snap(input);
        Assert.Equal(QuestionType.FillChips, q.FillMode);
        Assert.Equal(2, q.Blanks);
        Assert.Equal(1m, q.Score(["6", "4"]));
        Assert.Equal(0.5m, q.Score(["6", "10"]));
        Assert.Contains("fillMode", Check(input with { FillMode = "tap" }).Keys);
        Assert.Contains("ignoreDiacritics", Check(input with { IgnoreDiacritics = true }).Keys);
    }

    [Fact]
    public void An_essay_has_no_options_and_no_automatic_score()
    {
        Assert.Empty(Check(Essay()));
        Assert.Empty(Check(Essay("Vì cả hai đều chẵn")));
        Assert.Contains("options", Check(Essay() with { Options = Four }).Keys);
        Assert.Contains("sampleAnswer", Check(Essay(new string('x', 2001))).Keys);
        var q = Snap(Essay("mẫu", 3m));
        Assert.Equal("mẫu", q.SampleAnswer);
        Assert.Equal(1, q.Blanks);
        Assert.Equal(0m, q.Score(["một câu trả lời rất hay"]));
        Assert.False(q.IsCorrect(["một câu trả lời rất hay"]));
        Assert.False(QuestionType.AutoGraded(QuestionType.Essay));
    }

    [Fact]
    public void A_snapshot_stored_before_points_existed_reads_as_one_point_with_tap_a_button_and_accents_mattering()
    {
        const string legacy = """
            {"questionId":"11111111-1111-1111-1111-111111111111","code":"Q","type":"single_choice","level":"nhan_biet","stem":"s","stemImage":null,
             "options":[{"key":"A","text":"1","image":null},{"key":"B","text":"2","image":null}],"answer":[["B"]],"primarySkillId":"22222222-2222-2222-2222-222222222222"}
            """;
        var q = JsonSerializer.Deserialize<QuestionSnapshot>(legacy, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(1m, q.Points);
        Assert.Equal(QuestionType.FillChips, q.FillMode);
        Assert.False(q.IgnoreDiacritics);
        Assert.Equal(1m, q.Score(["B"]));
        Assert.Equal(0m, q.Score(["A"]));
    }
}
