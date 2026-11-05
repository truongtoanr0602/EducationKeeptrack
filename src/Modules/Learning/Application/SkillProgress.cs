using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Application;

public static class SkillProgress
{
    // Only independent, confirmed evidence with sufficient tasks qualifies.
    public static string Mastery(IEnumerable<SkillEvidence> evidence)
    {
        var latest = evidence.Where(x => x.Confirmed && x.Total >= 5).OrderByDescending(x => x.Date).ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Take(2).ToArray();
        return latest.Length < 2 ? "insufficient" : latest.All(x => (long)x.Correct * 100 >= (long)x.Total * 80) ? "achieved" : "needs_practice";
    }
}
