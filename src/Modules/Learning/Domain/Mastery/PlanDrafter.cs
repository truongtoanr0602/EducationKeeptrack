using System.Globalization;
namespace EducationKeeptrack.Learning.Domain;

public sealed record DraftSkill(Guid Id, string Code, string Name);
public sealed record DraftMaterial(Guid Id, Guid SkillId, string Title, int Minutes, string Difficulty);
public sealed record DraftStudent(Guid Id, string Name, IReadOnlyList<SkillBelief> Beliefs);

public sealed record DraftFacts(
    DateOnly Today,
    IReadOnlyList<PlanSession> Sessions,
    IReadOnlyList<DraftStudent> Students,
    IReadOnlyDictionary<Guid, DraftSkill> Skills,
    IReadOnlyList<DraftMaterial> ApprovedMaterials,
    MasteryRule Rule);

/// <summary>One scored candidate, kept so the teacher can see why a skill was or was not chosen.</summary>
public sealed record DraftTrace(Guid StudentId, Guid SkillId, string RuleState, int Attempts, double PKnown, double Score, bool Selected);

public sealed record PlanDraft(PlanContent Content, IReadOnlyList<DraftTrace> Trace);

/// <summary>What an explainer needs to word one goal. Only data already in the system; no names are sent outward.</summary>
public sealed record GoalFacts(string SkillCode, string SkillName, double PKnown, int Attempts, string RuleState, int ThresholdPercent, int MinTasks);

/// <summary>
/// The seam where a language model may later write the reason and re-check of a goal (BR-AI-15). It never chooses
/// skills, materials or minutes: those come from <see cref="PlanDrafter"/>, so changing the explainer cannot change the plan's shape.
/// </summary>
public interface IPlanExplainer
{
    string Name { get; }
    (string Reason, string Recheck) Explain(GoalFacts facts);
}

/// <summary>Deterministic wording from the model's numbers. Used when no language model is configured.</summary>
public sealed class TemplatePlanExplainer : IPlanExplainer
{
    public string Name => "template";

    public (string Reason, string Recheck) Explain(GoalFacts f)
    {
        var state = f.RuleState == CompetencyState.NotMastered ? "chưa đạt" : "chưa đủ dữ liệu";
        var evidence = f.Attempts == 0
            ? "chưa có kết quả đã xác nhận"
            : $"qua {f.Attempts} lần đánh giá đã xác nhận";
        // A two-state model cannot express "partly mastered": results that hover between the guess and slip rates can push it to
        // near certainty. Show that honestly and say why the rule still asks for review, instead of printing a contradiction.
        var percent = f.PKnown >= 0.995 ? "trên 99%" : (f.PKnown * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        var reason = $"Mô hình ước lượng xác suất nắm kỹ năng {f.SkillCode} {f.SkillName} là {percent} ({evidence}); theo quy tắc hiện tại kỹ năng này {state}.";
        if (f.PKnown >= 0.9 && f.RuleState == CompetencyState.NotMastered)
            reason += " Kết quả các lần gần đây dao động nên chưa đủ ngưỡng của quy tắc dù mô hình nghiêng về đã nắm; nên kiểm tra lại.";
        var recheck = $"Cho làm tối thiểu {f.MinTasks} nhiệm vụ cùng kỹ năng; đạt khi đúng từ {f.ThresholdPercent}%.";
        return (reason, recheck);
    }
}

/// <summary>
/// Builds a weekly review plan draft from the model's beliefs. Steps: the rule limits candidates; the model ranks them
/// and the top <see cref="PlanRules.MaxGoals"/> become goals; each session works one goal per student; students who share
/// the goal share one activity; the material is the approved one whose difficulty is nearest the student's level.
/// The caller must still pass the result through <see cref="PlanRules.Normalize"/> before it is shown or saved.
/// </summary>
public static class PlanDrafter
{
    private static readonly string[] Levels = ["basic", "standard", "advanced"];

    public static PlanDraft Build(DraftFacts facts, IPlanExplainer explainer)
    {
        var trace = new List<DraftTrace>();
        var students = new List<PlanStudent>();
        var goalsByStudent = new Dictionary<Guid, IReadOnlyList<RankedSkill>>();
        foreach (var student in facts.Students)
        {
            var ranked = SkillPriority.Rank(student.Beliefs, facts.Today, PlanRules.MaxGoals);
            var chosen = ranked.Select(x => x.SkillId).ToHashSet();
            foreach (var belief in student.Beliefs.Where(SkillPriority.IsCandidate))
                trace.Add(new(student.Id, belief.SkillId, belief.RuleState, belief.Attempts, belief.PKnown, SkillPriority.Score(belief, facts.Today).Score, chosen.Contains(belief.SkillId)));
            if (ranked.Count == 0) continue;
            goalsByStudent[student.Id] = ranked;
            var goals = ranked.Select(r =>
            {
                var belief = student.Beliefs.First(b => b.SkillId == r.SkillId);
                var skill = facts.Skills[r.SkillId];
                var (reason, recheck) = explainer.Explain(new(skill.Code, skill.Name, r.PKnown, belief.Attempts, belief.RuleState, facts.Rule.Threshold, facts.Rule.MinTasks));
                return new PlanGoal(r.SkillId, reason, recheck);
            }).ToArray();
            students.Add(new(student.Id, student.Name, goals));
        }

        var activities = new List<PlanActivity>();
        for (var s = 0; s < facts.Sessions.Count; s++)
        {
            var session = facts.Sessions[s];
            // Every student works exactly one goal per session, so nobody's load can pass the session's minutes.
            var groups = students.GroupBy(x => x.Goals![s % x.Goals!.Length].SkillId);
            foreach (var group in groups.OrderBy(x => x.Key))
            {
                var participants = group.Select(x => x.StudentId).ToArray();
                var pKnown = participants.Average(id => goalsByStudent[id].First(g => g.SkillId == group.Key).PKnown);
                var material = Choose(facts.ApprovedMaterials, group.Key, pKnown, session.Minutes);
                var skill = facts.Skills[group.Key];
                var minutes = Math.Clamp(material?.Minutes ?? session.Minutes, 1, session.Minutes);
                var description = material is null
                    ? $"Ôn kỹ năng {skill.Code} {skill.Name}. Chưa có học liệu đã duyệt cho kỹ năng này; giáo viên chọn bài tập."
                    : $"Ôn kỹ năng {skill.Code} {skill.Name} với học liệu \"{material.Title}\" ({material.Minutes} phút, mức {Vietnamese(material.Difficulty)}).";
                activities.Add(new(s, participants, group.Key, material?.Id, minutes, description));
            }
        }

        var note = "Dự thảo do mô hình theo dõi tri thức Bayes và bộ xếp hạng ưu tiên đề xuất. Giáo viên cần xem lại, sửa và duyệt trước khi áp dụng.";
        return new(new(facts.Sessions.ToArray(), students.ToArray(), activities.ToArray(), note), trace);
    }

    /// <summary>Weaker students get easier material: below 0.40 basic, below 0.75 standard, otherwise advanced.</summary>
    public static string TargetLevel(double pKnown) => pKnown < 0.40 ? "basic" : pKnown < 0.75 ? "standard" : "advanced";

    private static DraftMaterial? Choose(IReadOnlyList<DraftMaterial> materials, Guid skillId, double pKnown, int sessionMinutes)
    {
        var target = Array.IndexOf(Levels, TargetLevel(pKnown));
        return materials.Where(x => x.SkillId == skillId && x.Minutes <= sessionMinutes)
            .OrderBy(x => Math.Abs(Array.IndexOf(Levels, x.Difficulty) - target)).ThenBy(x => x.Minutes).ThenBy(x => x.Id).FirstOrDefault();
    }

    private static string Vietnamese(string level) => level switch { "basic" => "cơ bản", "advanced" => "nâng cao", _ => "vừa" };
}
