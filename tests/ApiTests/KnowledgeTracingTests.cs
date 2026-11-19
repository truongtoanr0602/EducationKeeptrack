using EducationKeeptrack.Learning.Domain;
namespace ApiTests;

// Bayesian Knowledge Tracing, the skill ranker and the plan drafter are pure code, so they are tested without a server.
public sealed class KnowledgeTracingTests
{
    private static readonly BktParams P = BktParams.Default;

    [Fact]
    public void The_binomial_likelihood_sums_to_one_over_the_outcomes()
    {
        var total = Enumerable.Range(0, 9).Sum(k => KnowledgeTracing.Likelihood(new(8, k), 0.37));
        Assert.Equal(1.0, total, 9);
    }

    [Fact]
    public void Right_answers_raise_the_belief_and_wrong_answers_lower_it()
    {
        Assert.True(KnowledgeTracing.Update(0.5, new(5, 5), P) > 0.5);
        Assert.True(KnowledgeTracing.Update(0.5, new(5, 0), P) < 0.5);
        Assert.True(KnowledgeTracing.Update(0.5, new(5, 4), P) > KnowledgeTracing.Update(0.5, new(5, 2), P));
    }

    [Fact]
    public void More_good_attempts_give_a_higher_belief_and_the_trace_stays_a_probability()
    {
        var one = KnowledgeTracing.Current([new(5, 5)], P);
        var three = KnowledgeTracing.Current([new(5, 5), new(5, 5), new(5, 5)], P);
        Assert.True(three > one);
        Assert.Equal(P.PInit, KnowledgeTracing.Current([], P));
        Assert.All(KnowledgeTracing.Trace([new(5, 2), new(5, 3), new(5, 5)], P), x => Assert.InRange(x, 0.0, 1.0));
    }

    [Fact]
    public void A_pass_is_likelier_for_a_student_who_probably_knows_the_skill()
    {
        var weak = KnowledgeTracing.PassProbability(5, 0.8, KnowledgeTracing.ExpectedRate(0.1, P));
        var strong = KnowledgeTracing.PassProbability(5, 0.8, KnowledgeTracing.ExpectedRate(0.9, P));
        Assert.True(strong > weak);
        Assert.InRange(weak, 0.0, 1.0);
    }

    /// <summary>Draws attempts from a known model so that fitting can be checked against the truth.</summary>
    private static List<IReadOnlyList<Attempt>> Simulate(BktParams truth, int sequences, int length, int tasks, int seed)
    {
        var random = new Random(seed);
        var list = new List<IReadOnlyList<Attempt>>();
        for (var s = 0; s < sequences; s++)
        {
            var known = random.NextDouble() < truth.PInit;
            var attempts = new List<Attempt>();
            for (var t = 0; t < length; t++)
            {
                var rate = known ? 1 - truth.PSlip : truth.PGuess;
                attempts.Add(new(tasks, Enumerable.Range(0, tasks).Count(_ => random.NextDouble() < rate)));
                if (!known && random.NextDouble() < truth.PLearn) known = true;
            }
            list.Add(attempts);
        }
        return list;
    }

    [Fact]
    public void Fitting_improves_the_likelihood_and_moves_towards_the_true_parameters()
    {
        var truth = new BktParams(0.20, 0.30, 0.15, 0.08);
        var data = Simulate(truth, 300, 6, 8, seed: 7);
        var fitted = KnowledgeTracing.Fit(data);
        Assert.True(fitted.IsValid);
        Assert.True(KnowledgeTracing.LogLikelihood(data, fitted) >= KnowledgeTracing.LogLikelihood(data, BktParams.Default));
        // The fit need not hit the truth exactly, but it must be nearer than the default is for the parameters the data pin down.
        Assert.InRange(Math.Abs(fitted.PGuess - truth.PGuess), 0, 0.10);
        Assert.InRange(Math.Abs(fitted.PSlip - truth.PSlip), 0, 0.10);
    }

    [Fact]
    public void Fitting_with_no_data_keeps_the_start()
    {
        Assert.Equal(BktParams.Default, KnowledgeTracing.Fit([]));
    }

    private static SkillBelief Belief(int n, double p, string state = CompetencyState.NotMastered, int attempts = 2, int? daysAgo = 7) =>
        new(Guid.Parse($"00000000-0000-0000-0000-{n:D12}"), p, attempts, daysAgo is { } d ? new DateOnly(2026, 10, 12).AddDays(-d) : null, state);

    private static readonly DateOnly Today = new(2026, 10, 12);

    [Fact]
    public void The_ranker_skips_mastered_and_untaught_skills_and_puts_the_weakest_first()
    {
        var beliefs = new[]
        {
            Belief(1, 0.90), Belief(2, 0.20), Belief(3, 0.55, CompetencyState.Insufficient, 1),
            Belief(4, 0.99, CompetencyState.Mastered), Belief(5, 0.10, CompetencyState.NotTaught)
        };
        var ranked = SkillPriority.Rank(beliefs, Today, 2);
        Assert.Equal(2, ranked.Count);
        Assert.Equal(beliefs[1].SkillId, ranked[0].SkillId);
        Assert.DoesNotContain(ranked, x => x.SkillId == beliefs[3].SkillId || x.SkillId == beliefs[4].SkillId);
    }

    [Fact]
    public void An_equal_pair_keeps_a_stable_order_and_an_older_attempt_ranks_higher()
    {
        var a = Belief(1, 0.5); var b = Belief(2, 0.5);
        Assert.Equal([a.SkillId, b.SkillId], SkillPriority.Rank([b, a], Today, 2).Select(x => x.SkillId));
        var recent = Belief(3, 0.5, daysAgo: 1); var old = Belief(4, 0.5, daysAgo: 25);
        Assert.Equal(old.SkillId, SkillPriority.Rank([recent, old], Today, 1)[0].SkillId);
    }

    private static readonly Guid A1 = Guid.Parse("10000000-0000-0000-0000-000000000001"), A2 = Guid.Parse("10000000-0000-0000-0000-000000000002");

    private static DraftFacts Facts(params DraftStudent[] students) => new(
        Today, [new(new(2026, 10, 13), 30, null), new(new(2026, 10, 15), 30, null)], students,
        new Dictionary<Guid, DraftSkill> { [A1] = new(A1, "A1", "Cộng trừ"), [A2] = new(A2, "A2", "Nhân chia") },
        [new(Guid.Parse("20000000-0000-0000-0000-000000000001"), A1, "Phiếu cơ bản", 20, "basic"),
         new(Guid.Parse("20000000-0000-0000-0000-000000000002"), A1, "Phiếu nâng cao", 20, "advanced"),
         new(Guid.Parse("20000000-0000-0000-0000-000000000003"), A2, "Phiếu vừa", 40, "standard")],
        new(80, 5, 2, 1));

    private static SkillBelief B(Guid skill, double p, string state = CompetencyState.NotMastered) => new(skill, p, 2, new DateOnly(2026, 10, 5), state);

    [Fact]
    public void The_draft_gives_each_student_at_most_two_goals_and_shares_an_activity_between_students_with_the_same_goal()
    {
        var an = new DraftStudent(Guid.NewGuid(), "An", [B(A1, 0.2), B(A2, 0.9, CompetencyState.Mastered)]);
        var binh = new DraftStudent(Guid.NewGuid(), "Bình", [B(A1, 0.3), B(A2, 0.9, CompetencyState.Mastered)]);
        var draft = PlanDrafter.Build(Facts(an, binh), new TemplatePlanExplainer());

        Assert.All(draft.Content.Students!, s => Assert.InRange(s.Goals!.Length, 1, PlanRules.MaxGoals));
        Assert.All(draft.Content.Students!, s => Assert.Equal([A1], s.Goals!.Select(g => g.SkillId)));
        var first = draft.Content.Activities!.Where(x => x.Session == 0).ToList();
        Assert.Single(first);
        Assert.Equal(2, first[0].StudentIds!.Length);
        Assert.Contains(draft.Trace, x => x.SkillId == A1 && x.Selected);
        Assert.DoesNotContain(draft.Trace, x => x.SkillId == A2);
    }

    [Fact]
    public void The_material_follows_the_level_of_the_student_and_never_exceeds_the_session()
    {
        var weak = new DraftStudent(Guid.NewGuid(), "Yếu", [B(A1, 0.1)]);
        var strong = new DraftStudent(Guid.NewGuid(), "Khá", [B(A1, 0.8, CompetencyState.Insufficient)]);
        var forWeak = PlanDrafter.Build(Facts(weak), new TemplatePlanExplainer()).Content.Activities![0];
        var forStrong = PlanDrafter.Build(Facts(strong), new TemplatePlanExplainer()).Content.Activities![0];
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000001"), forWeak.MaterialId);
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000002"), forStrong.MaterialId);
        Assert.All(new[] { forWeak, forStrong }, x => Assert.InRange(x.Minutes, 1, 30));

        // A2's only material takes 40 minutes, longer than a 30 minute session, so no material is chosen and the teacher is told.
        var onlyLong = new DraftStudent(Guid.NewGuid(), "Chỉ nhân chia", [B(A2, 0.2)]);
        var activity = PlanDrafter.Build(Facts(onlyLong), new TemplatePlanExplainer()).Content.Activities![0];
        Assert.Null(activity.MaterialId);
        Assert.Contains("Chưa có học liệu", activity.Description);
    }

    [Fact]
    public void The_draft_passes_the_same_rules_as_a_hand_made_plan_including_approval()
    {
        var an = new DraftStudent(Guid.NewGuid(), "An", [B(A1, 0.2), B(A2, 0.4)]);
        var facts = Facts(an);
        var draft = PlanDrafter.Build(facts, new TemplatePlanExplainer());
        var context = new PlanContext(new(2026, 10, 12), new Dictionary<Guid, string> { [an.Id] = "An" }, new HashSet<Guid> { A1, A2 },
            facts.ApprovedMaterials.ToDictionary(x => x.Id, x => x.SkillId), new Dictionary<Guid, DateOnly>());
        var (content, errors) = PlanRules.Normalize(draft.Content, context, forApproval: true);
        Assert.Null(errors);
        Assert.NotNull(content);
    }

    [Fact]
    public void The_explanation_does_not_contradict_itself_when_the_model_is_sure_but_the_rule_says_not_mastered()
    {
        var (reason, recheck) = new TemplatePlanExplainer().Explain(new("A1", "Cộng trừ", 1.0, 4, CompetencyState.NotMastered, 80, 5));
        Assert.Contains("trên 99%", reason);
        Assert.DoesNotContain("100%", reason);
        Assert.Contains("kiểm tra lại", reason);
        Assert.InRange(reason.Length, 1, 500);
        Assert.InRange(recheck.Length, 1, 300);
        var (plain, _) = new TemplatePlanExplainer().Explain(new("A1", "Cộng trừ", 0.42, 4, CompetencyState.NotMastered, 80, 5));
        Assert.Contains("42%", plain);
        Assert.DoesNotContain("dao động", plain);
    }

    [Fact]
    public void Nobody_to_review_gives_an_empty_draft_the_service_can_refuse()
    {
        var done = new DraftStudent(Guid.NewGuid(), "Xong", [B(A1, 0.99, CompetencyState.Mastered)]);
        Assert.Empty(PlanDrafter.Build(Facts(done), new TemplatePlanExplainer()).Content.Students!);
    }
}
