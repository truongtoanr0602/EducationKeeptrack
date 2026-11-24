namespace EducationKeeptrack.Learning.Domain;

/// <summary>Sheet names of the Excel template; the parser and the error messages use the same names.</summary>
public static class ImportSheets
{
    public const string Guide = "Hướng dẫn", Info = "Thông tin chung", Skills = "Kỹ năng", Lessons = "Buổi", Questions = "Câu hỏi";
}

/// <summary>One problem found while reading or applying a workbook; Row is the 1-based Excel row.</summary>
public sealed record ImportIssue(string Sheet, int Row, string Column, string Message);

public sealed record ImportInfo(string? Subject, string? Grade, string? Term, string? Textbook);
public sealed record SkillImportRow(int Row, string Code, string Name, string Strand, string? Criteria);
public sealed record LessonImportRow(int Row, int No, string Topic, string Title, string Knowledge, string Outcomes, string SkillsText, string Competencies, string[] SkillCodes);
public sealed record QuestionImportRow(int Row, string Code, string Type, string Level, string PrimarySkill, string[] SecondarySkills, int? LessonNo,
    string Stem, string? StemImage, string[] Options, string[] Chips, string Answer, string Points = "");

/// <summary>What a workbook holds after reading, before it is checked against the class.</summary>
public sealed record ImportBundle(ImportInfo Info, IReadOnlyList<SkillImportRow> Skills, IReadOnlyList<LessonImportRow> Lessons,
    IReadOnlyList<QuestionImportRow> Questions, IReadOnlyList<ImportIssue> Issues);
