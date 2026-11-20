using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
namespace ApiTests;

/// <summary>Pure rules of lessons, the question bank, exams and the pooled mastery rule (#78–#82); no database needed.</summary>
public sealed class QuestionBankRuleTests
{
    private static readonly Guid E1 = Guid.NewGuid(), E2 = Guid.NewGuid(), E3 = Guid.NewGuid();
    private static readonly HashSet<Guid> Live = [E1, E2, E3];
    private static readonly HashSet<string> NoImages = [];

    private static QuestionInput Choice(string code, string level, Guid skill, string answer = "B") =>
        new(code, QuestionType.SingleChoice, level, skill, [], null, "Số gồm 6 chục và 4 đơn vị là:", null,
            [new("A", "46", null), new("B", "64", null), new("C", "604", null)], [[answer]], false, 0);

    private static QuestionInput Blanks(string code, string stem, string[] chips, string[][] answer) =>
        new(code, QuestionType.FillBlanks, QuestionLevel.Recall, E1, [], null, stem, null,
            chips.Select((x, i) => new QuestionOption((i + 1).ToString(), x, null)).ToArray(), answer, false, 0);

    private static Question Make(QuestionInput input) => new("owner", Guid.NewGuid(), input, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Level_labels_and_everyday_words_parse_to_the_four_fixed_levels()
    {
        Assert.Equal(QuestionLevel.Recall, QuestionLevel.Parse("Nhận biết"));
        Assert.Equal(QuestionLevel.Recall, QuestionLevel.Parse("cơ bản"));
        Assert.Equal(QuestionLevel.Understand, QuestionLevel.Parse("Trung bình"));
        Assert.Equal(QuestionLevel.Apply, QuestionLevel.Parse("VẬN DỤNG"));
        Assert.Equal(QuestionLevel.Advanced, QuestionLevel.Parse("Vận dụng cao"));
        Assert.Equal(QuestionLevel.Advanced, QuestionLevel.Parse("van_dung_cao"));
        Assert.Null(QuestionLevel.Parse("khó"));
        Assert.Equal(QuestionType.FillBlanks, QuestionType.Parse("Điền ô"));
        Assert.Equal(QuestionType.SingleChoice, QuestionType.Parse("trắc nghiệm"));
    }

    [Fact]
    public void A_fill_in_question_is_right_only_when_every_blank_holds_an_accepted_value()
    {
        var input = Blanks("Q1", "6 chục 4 đơn vị: [ ] chục [ ] đơn vị", ["4", "6", "10"], [["6"], ["4"]]);
        Assert.Empty(Question.Validate(input, Live, NoImages));
        var snapshot = Make(input).Snapshot();
        Assert.Equal(2, snapshot.Blanks);
        Assert.True(snapshot.IsCorrect(["6", " 4 "]));
        Assert.False(snapshot.IsCorrect(["6", "6"]));
        Assert.False(snapshot.IsCorrect(["6"]));
        Assert.False(snapshot.IsCorrect(null));

        var alternatives = Make(Blanks("Q2", "Số liền sau của 9 là [ ]", ["10", "mười", "8"], [["10", "mười"]])).Snapshot();
        Assert.True(alternatives.IsCorrect(["Mười"]));
    }

    [Fact]
    public void Validation_names_each_broken_part_of_a_question()
    {
        var wrongBlanks = Question.Validate(Blanks("Q1", "8 + 5 = [ ] và [ ]", ["13", "12"], [["13"]]), Live, NoImages);
        Assert.Contains("answer", wrongBlanks.Keys);
        var notAChip = Question.Validate(Blanks("Q1", "8 + 5 = [ ]", ["12", "14"], [["13"]]), Live, NoImages);
        Assert.Contains("answer", notAChip.Keys);
        var noBlank = Question.Validate(Blanks("Q1", "8 + 5 = ?", ["13", "12"], []), Live, NoImages);
        Assert.Contains("stem", noBlank.Keys);
        var unknownSkill = Question.Validate(Choice("Q1", QuestionLevel.Recall, Guid.NewGuid()), Live, NoImages);
        Assert.Contains("primarySkillId", unknownSkill.Keys);
        var badAnswer = Question.Validate(Choice("Q1", QuestionLevel.Recall, E1, "D"), Live, NoImages);
        Assert.Contains("answer", badAnswer.Keys);
        var missingImage = Question.Validate(Choice("Q1", QuestionLevel.Recall, E1) with { StemImage = "dongho3.png" }, Live, NoImages);
        Assert.Contains("stemImage", missingImage.Keys);
        Assert.Empty(Question.Validate(Choice("Q1", QuestionLevel.Recall, E1) with { StemImage = "DongHo3.PNG" }, Live, new HashSet<string> { "dongho3.png" }));
    }

    [Fact]
    public void Drawing_covers_the_skills_of_a_level_evenly_and_never_borrows_from_another_level()
    {
        var bank = new List<Question>();
        foreach (var skill in new[] { E1, E2, E3 })
            for (var i = 0; i < 4; i++) bank.Add(Make(Choice($"{skill:N}-{i}", QuestionLevel.Recall, skill)));
        bank.Add(Make(Choice("U1", QuestionLevel.Understand, E1)));

        var (picked, shortfalls) = ExamGenerator.Draw(bank, [6, 1, 0, 0], new Random(7));
        Assert.Empty(shortfalls);
        Assert.Equal(7, picked.Count);
        Assert.Equal(7, picked.Select(x => x.Id).Distinct().Count());
        var recall = picked.Where(x => x.Level == QuestionLevel.Recall).GroupBy(x => x.PrimarySkillId).ToDictionary(g => g.Key, g => g.Count());
        Assert.All(new[] { E1, E2, E3 }, skill => Assert.Equal(2, recall[skill]));

        var (none, missing) = ExamGenerator.Draw(bank, [4, 3, 2, 1], new Random(7));
        Assert.Empty(none);
        Assert.Equal([QuestionLevel.Understand, QuestionLevel.Apply, QuestionLevel.Advanced], missing.Select(x => x.Level));
        Assert.Equal((3, 1), (missing[0].Needed, missing[0].Available));
    }

    [Fact]
    public void Result_levels_take_the_nearest_of_the_four_and_round_ties_up()
    {
        Assert.Equal(0, ResultLevels.Nearest(0, 10));
        Assert.Equal(25, ResultLevels.Nearest(1, 10));
        Assert.Equal(75, ResultLevels.Nearest(7, 10));
        Assert.Equal(75, ResultLevels.Nearest(6, 8));
        Assert.Equal(50, ResultLevels.Nearest(5, 10));
        Assert.Equal(75, ResultLevels.Nearest(5, 8)); // 62.5 lies between 50 and 75
        Assert.Equal(100, ResultLevels.Nearest(9, 10));
    }

    [Fact]
    public void Lessons_pair_with_sessions_in_order_skipping_cancelled_ones_and_manual_links_win()
    {
        var curriculum = new ClassCurriculum("owner", Guid.NewGuid(), new("Toán", "Lớp 2", "HK1", "KNTT", 80, 5, 2, 0), DateTimeOffset.UnixEpoch);
        var rows = Enumerable.Range(1, 3).Select(n => new LessonImportRow(n, n, "", $"Buổi {n}", "", "", "", "", [])).ToList();
        Assert.Empty(curriculum.ImportLessons(rows, DateTimeOffset.UnixEpoch).Issues);
        var lessons = curriculum.Lessons;
        var d = new DateOnly(2026, 9, 7);
        ScheduledSession S(int day, string status) => new(Guid.NewGuid(), d.AddDays(day), new(17, 0), status);
        var sessions = new[] { S(0, "held"), S(2, "cancelled"), S(4, "held"), S(7, "planned") };

        var auto = LessonSchedule.Pair(lessons, sessions, []);
        Assert.Equal([sessions[0].Id, sessions[2].Id, sessions[3].Id], auto.Select(x => x.Session!.Id));

        var link = new LessonSessionLink("owner", curriculum.ClassId, lessons[0].Id, sessions[3].Id, DateTimeOffset.UnixEpoch);
        var manual = LessonSchedule.Pair(lessons, sessions, [link]);
        Assert.True(manual[0].Manual);
        Assert.Equal(sessions[3].Id, manual[0].Session!.Id);
        Assert.Equal([sessions[0].Id, sessions[2].Id], manual.Skip(1).Select(x => x.Session!.Id));
    }

    [Fact]
    public void Exam_results_are_pooled_until_they_reach_min_tasks()
    {
        var rule = new MasteryRule(80, 5, 2, 2);
        ConfirmedResult Exam(int day, int tasks, int correct) =>
            new(Guid.NewGuid(), "Đề", new DateOnly(2026, 10, 1).AddDays(day), SkillResultStatus.Assessed, tasks, correct, ResultSource.Exam);

        var thin = Competency.Evaluate(true, rule, [Exam(1, 2, 2), Exam(2, 2, 2)]);
        Assert.Equal(CompetencyState.Insufficient, thin.State);
        Assert.All(thin.Evidence, x => Assert.True(x.Counted));

        Assert.Equal(CompetencyState.Mastered, Competency.Evaluate(true, rule, [Exam(1, 2, 2), Exam(2, 2, 2), Exam(3, 2, 1)]).State);
        Assert.Equal(CompetencyState.NotMastered, Competency.Evaluate(true, rule, [Exam(1, 2, 1), Exam(2, 2, 1), Exam(3, 2, 2)]).State);

        // The window holds MinTasks × RequiredAttempts questions: old weak answers drop out once enough new ones exist.
        var old = Enumerable.Range(0, 5).Select(i => Exam(i, 2, 0));
        var recent = Enumerable.Range(10, 5).Select(i => Exam(i, 2, 2));
        var result = Competency.Evaluate(true, rule, [.. old, .. recent]);
        Assert.Equal(CompetencyState.Mastered, result.State);
        Assert.Equal(5, result.Evidence.Count(x => x.Counted));
    }

    [Fact]
    public void Manual_results_keep_rule_34_and_the_newer_source_decides_when_both_conclude()
    {
        var rule = new MasteryRule(80, 5, 2, 2);
        ConfirmedResult Manual(int day, int correct) => new(Guid.NewGuid(), "Bài", new DateOnly(2026, 10, 1).AddDays(day), SkillResultStatus.Assessed, 5, correct);
        ConfirmedResult Exam(int day, int correct) => new(Guid.NewGuid(), "Đề", new DateOnly(2026, 10, 1).AddDays(day), SkillResultStatus.Assessed, 5, correct, ResultSource.Exam);

        Assert.Equal(CompetencyState.Mastered, Competency.Evaluate(true, rule, [Manual(1, 5), Manual(2, 4)]).State);
        Assert.Equal(CompetencyState.NotMastered, Competency.Evaluate(true, rule, [Manual(1, 5), Manual(2, 4), Exam(5, 1)]).State);
        Assert.Equal(CompetencyState.Mastered, Competency.Evaluate(true, rule, [Exam(1, 1), Manual(4, 5), Manual(5, 5)]).State);
        Assert.Equal(CompetencyState.NotTaught, Competency.Evaluate(false, rule, [Exam(1, 5)]).State);
    }

    [Fact]
    public void Seat_grading_counts_right_answers_per_primary_skill_and_never_shows_the_order_twice_the_same_way_by_accident()
    {
        var questions = new[]
        {
            Make(Blanks("A", "8 + 5 = [ ]", ["12", "13"], [["13"]])),
            Make(Choice("B", QuestionLevel.Recall, E2)),
            Make(Choice("C", QuestionLevel.Recall, E2))
        };
        var exam = new Exam("owner", Guid.NewGuid(), "Đề 1", [3, 0, 0, 0], new(1, 3, []), 30, questions.Select(x => x.Snapshot()), DateTimeOffset.UnixEpoch);
        var student = Guid.NewGuid();
        Assert.Equal(200, exam.Open(Guid.NewGuid(), [(student, "Nguyễn Văn An")], 1, DateTimeOffset.UnixEpoch).Status);
        var seat = exam.Seats.Single();
        Assert.Equal(JoinOutcome.WrongCode, seat.Join(exam, "xxxx", DateTimeOffset.UnixEpoch));
        Assert.Equal(JoinOutcome.Ok, seat.Join(exam, seat.Code, DateTimeOffset.UnixEpoch));
        Assert.Equal(AttemptStatus.InProgress, seat.AttemptStatus);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(30), seat.Deadline);

        Assert.Equal(200, seat.SaveAnswer(exam, 1, ["13"], DateTimeOffset.UnixEpoch).Status);
        Assert.Equal(200, seat.SaveAnswer(exam, 2, ["B"], DateTimeOffset.UnixEpoch).Status);
        Assert.Equal(200, seat.SaveAnswer(exam, 3, ["A"], DateTimeOffset.UnixEpoch).Status);
        Assert.Equal(400, seat.SaveAnswer(exam, 1, ["13", "1"], DateTimeOffset.UnixEpoch).Status);

        Assert.True(seat.ExpireIfDue(exam, DateTimeOffset.UnixEpoch.AddMinutes(31)));
        Assert.Equal(AttemptStatus.AutoSubmitted, seat.AttemptStatus);
        Assert.Equal(2, seat.CorrectCount);
        var breakdown = seat.SkillBreakdown(exam);
        Assert.Equal((1, 1), breakdown[E1]);
        Assert.Equal((2, 1), breakdown[E2]);
        Assert.Equal(3, seat.PresentedItems(exam).Select(x => x.Order).Distinct().Count());
        Assert.Equal(400, seat.SaveAnswer(exam, 2, ["A"], DateTimeOffset.UnixEpoch.AddMinutes(32)).Status);
    }

    [Fact]
    public void Five_wrong_codes_lock_the_seat_until_the_teacher_resets_it()
    {
        var exam = new Exam("owner", Guid.NewGuid(), "Đề 1", [1, 0, 0, 0], new(1, 1, []), null, [Make(Choice("A", QuestionLevel.Recall, E1)).Snapshot()], DateTimeOffset.UnixEpoch);
        var student = Guid.NewGuid();
        exam.Open(Guid.NewGuid(), [(student, "An")], 1, DateTimeOffset.UnixEpoch);
        var seat = exam.Seats.Single();
        for (var i = 0; i < 4; i++) Assert.Equal(JoinOutcome.WrongCode, seat.Join(exam, "zzzz", DateTimeOffset.UnixEpoch));
        Assert.Equal(JoinOutcome.Locked, seat.Join(exam, "zzzz", DateTimeOffset.UnixEpoch));
        Assert.Equal(JoinOutcome.Locked, seat.Join(exam, seat.Code, DateTimeOffset.UnixEpoch));
        Assert.Equal(200, exam.ResetSeat(student, exam.Version).Status);
        Assert.Equal(JoinOutcome.Ok, seat.Join(exam, seat.Code, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void The_downloadable_template_reads_back_as_the_whole_sample_without_issues()
    {
        using var stream = new MemoryStream(WorkbookFormat.BuildTemplate());
        var bundle = WorkbookFormat.Read(stream);
        Assert.Empty(bundle.Issues);
        Assert.Equal("Toán", bundle.Info.Subject);
        Assert.Equal(9, bundle.Skills.Count);
        Assert.Equal(30, bundle.Lessons.Count);
        Assert.Equal(Enumerable.Range(1, 30), bundle.Lessons.Select(x => x.No));
        Assert.Equal(["E3", "E7"], bundle.Lessons[3].SkillCodes);
        Assert.Equal(Toan2Hk1Sample.Questions.Length, bundle.Questions.Count);
        var fill = bundle.Questions.Single(x => x.Code == "T2-002");
        Assert.Equal([">", "<", "="], fill.Chips);
        Assert.Equal("<", fill.Answer);
    }

    [Fact]
    public void Display_names_show_the_given_name_and_the_family_initial_only()
    {
        Assert.Equal("An N.", EducationKeeptrack.Learning.Application.ExamTakeService.DisplayName("Nguyễn Văn An"));
        Assert.Equal("Bình", EducationKeeptrack.Learning.Application.ExamTakeService.DisplayName("Bình"));
    }
}
