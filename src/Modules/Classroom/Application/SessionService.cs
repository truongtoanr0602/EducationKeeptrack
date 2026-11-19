using EducationKeeptrack.Classroom.Domain;
using EducationKeeptrack.Contracts;
namespace EducationKeeptrack.Classroom.Application;

public record ScheduleView(int Version, DateOnly? EffectiveFrom, IReadOnlyList<ScheduleSlotInput> Slots)
{
    public static ScheduleView From(WeeklySchedule? schedule) => schedule is null ? new(0, null, [])
        : new(schedule.Version, schedule.EffectiveFrom, schedule.Slots.OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).Select(x => new ScheduleSlotInput(x.DayOfWeek, x.StartTime, x.EndTime)).ToArray());
}
public record SessionView(Guid Id, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string Status, string Kind, int? ScheduleVersion, int Version, string TimeZone, Guid? ReplacesSessionId, string? Reason)
{
    public static SessionView From(ClassSession item) => new(item.Id, item.Date, item.StartTime, item.EndTime, item.StartsAtUtc, item.EndsAtUtc, item.Status, item.Kind, item.ScheduleVersion, item.Version, ClassSession.TimeZone, item.ReplacesSessionId, item.Reason);
}
public record AttendanceView(Guid StudentId, string Name, string Status, string? Reason, bool Excused, int Version, int SessionVersion);
public record AttendanceWithStudent(Attendance Attendance, Student Student);
public record TodaySession(Guid ClassId, string ClassName, string Subject, SessionView Session, int Students, int Present, int Absent);
/// <summary>One class's weekly schedule in effect, for the cross-class week view.</summary>
public record ClassScheduleView(Guid ClassId, string ClassName, DateOnly EffectiveFrom, IReadOnlyList<ScheduleSlotInput> Slots);
public record TodayView(DateOnly Date, IReadOnlyList<TodaySession> Sessions);
public record SessionWithClass(ClassSession Session, string ClassName, string Subject);
public record MarkRow(Guid SessionId, Guid StudentId, string Status);
public record MonthSummary(string Month, int Held, int Planned, int Cancelled, int Present, int Absent, double? AttendanceRate);
public record SessionStatusRow(Guid Id, string Status);
public record MissingAttendance(Guid SessionId, DateOnly Date, TimeOnly StartTime, Guid StudentId, string Name);

public interface ISessionRepository
{
    Task<bool> ClassExistsAsync(string owner, Guid classId, CancellationToken ct);
    Task<IReadOnlyList<WeeklySchedule>> SchedulesAsync(string owner, Guid classId, CancellationToken ct);
    Task<IReadOnlyList<ClassSession>> SessionsAsync(string owner, Guid classId, SessionRange range, CancellationToken ct);
    Task<ClassSession?> FindSessionAsync(string owner, Guid classId, Guid id, CancellationToken ct);
    Task<bool> HasReplacementAsync(string owner, Guid classId, Guid sourceId, CancellationToken ct);
    Task<IReadOnlyList<Student>> EligibleStudentsAsync(string owner, Guid classId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<AttendanceWithStudent>> AttendanceAsync(string owner, Guid sessionId, CancellationToken ct);
    /// <summary>For each of the owner's classes, its latest schedule revision already in effect on <paramref name="date"/>.</summary>
    Task<IReadOnlyList<(string ClassName, WeeklySchedule Schedule)>> ActiveSchedulesAsync(string owner, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<SessionWithClass>> SessionsOnAsync(string owner, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<SessionStatusRow>> StatusesAsync(string owner, DateOnly from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<MarkRow>> MarksAsync(string owner, IReadOnlyCollection<Guid> sessionIds, CancellationToken ct);
    Task<IReadOnlyList<(Guid ClassId, Guid StudentId)>> EligibleAsync(string owner, IReadOnlyCollection<Guid> classIds, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<SessionChange>> ChangesAsync(string owner, Guid sessionId, CancellationToken ct);
    void Add(WeeklySchedule schedule);
    void Add(ClassSession session);
    void Add(Attendance attendance);
    void Add(SessionChange change);
    Task SaveAsync(CancellationToken ct);
    Task<ClassroomResult<T>> WithClassLockAsync<T>(Guid classId, Func<Task<ClassroomResult<T>>> action, CancellationToken ct) where T : class;
}

public sealed class SessionService(ISessionRepository repository, TimeProvider clock)
{
    public async Task<ClassroomResult<SessionView>> CreateAsync(string owner, Guid classId, SessionInput input, CancellationToken ct)
    {
        if (!await repository.ClassExistsAsync(owner, classId, ct)) return new(null, ClassroomFailure.NotFound);
        var errors = input.Validate();
        if (errors.Count > 0) return new(null, ClassroomFailure.Invalid, Errors: errors);
        return await repository.WithClassLockAsync<SessionView>(classId, async () =>
        {
            if (input.ReplacesSessionId is { } sourceId)
            {
                var source = await repository.FindSessionAsync(owner, classId, sourceId, ct);
                if (source is null) return new(null, ClassroomFailure.NotFound);
                if (source.Status != "cancelled" || await repository.HasReplacementAsync(owner, classId, sourceId, ct)) return new(null, ClassroomFailure.Conflict, "Buổi gốc chưa hủy hoặc đã có buổi học bù đang hiệu lực.");
            }
            var existing = await repository.SessionsAsync(owner, classId, new(input.Date, input.Date), ct);
            if (existing.Any(x => x.Overlaps(input.Date, input.StartTime, input.EndTime))) return new(null, ClassroomFailure.Conflict, "Khung giờ trùng với buổi đã lưu, kể cả buổi đã hủy. Vui lòng chọn khung giờ khác.");
            var item = new ClassSession(owner, classId, input.Date, input.StartTime, input.EndTime, null, input.Kind, input.ReplacesSessionId);
            repository.Add(item); repository.Add(new SessionChange(item, "session.created", clock.GetUtcNow())); await repository.SaveAsync(ct);
            return new(SessionView.From(item));
        }, ct);
    }
    public async Task<ClassroomResult<SessionView>> ChangeStatusAsync(string owner, Guid classId, Guid id, SessionStatusInput input, CancellationToken ct) =>
        await repository.WithClassLockAsync<SessionView>(classId, async () =>
        {
            var item = await repository.FindSessionAsync(owner, classId, id, ct);
            if (item is null) return new(null, ClassroomFailure.NotFound);
            var problem = item.TryChangeStatus(input);
            if (problem is not null) return new(null, problem.IsConflict ? ClassroomFailure.Conflict : ClassroomFailure.Invalid, problem.Message, problem.Errors);
            repository.Add(new SessionChange(item, "session.statusChanged", clock.GetUtcNow()));
            await repository.SaveAsync(ct);
            return new(SessionView.From(item));
        }, ct);
    public async Task<ClassroomResult<ScheduleView>> ScheduleAsync(string owner, Guid classId, CancellationToken ct)
    {
        if (!await repository.ClassExistsAsync(owner, classId, ct)) return new(null, ClassroomFailure.NotFound);
        return new(ScheduleView.From((await repository.SchedulesAsync(owner, classId, ct)).LastOrDefault()));
    }
    public async Task<ClassroomResult<ScheduleView>> SaveScheduleAsync(string owner, Guid classId, ScheduleInput input, CancellationToken ct)
    {
        if (!await repository.ClassExistsAsync(owner, classId, ct)) return new(null, ClassroomFailure.NotFound);
        var errors = input.Validate();
        if (errors.Count > 0) return new(null, ClassroomFailure.Invalid, Errors: errors);
        return await repository.WithClassLockAsync<ScheduleView>(classId, async () =>
        {
            var latest = (await repository.SchedulesAsync(owner, classId, ct)).LastOrDefault();
            if (input.Version != (latest?.Version ?? 0)) return new(null, ClassroomFailure.Conflict, "Lịch tuần đã thay đổi. Vui lòng tải lại trước khi lưu tiếp.");
            if (latest is not null && input.EffectiveFrom < latest.EffectiveFrom) return new(null, ClassroomFailure.Invalid, Errors: new() { ["effectiveFrom"] = ["Ngày hiệu lực không được trước phiên bản lịch gần nhất."] });
            var schedule = new WeeklySchedule(owner, classId, input, clock.GetUtcNow());
            repository.Add(schedule); await repository.SaveAsync(ct);
            return new(ScheduleView.From(schedule));
        }, ct);
    }
    public async Task<ClassroomResult<IReadOnlyList<SessionView>>> ListAsync(string owner, Guid classId, SessionRange range, CancellationToken ct)
    {
        if (!await repository.ClassExistsAsync(owner, classId, ct)) return new(null, ClassroomFailure.NotFound);
        var errors = range.Validate();
        if (errors.Count > 0) return new(null, ClassroomFailure.Invalid, Errors: errors);
        return new((await repository.SessionsAsync(owner, classId, range, ct)).Select(SessionView.From).ToArray());
    }
    public async Task<ClassroomResult<IReadOnlyList<SessionView>>> GenerateAsync(string owner, Guid classId, GenerateSessionsInput input, CancellationToken ct)
    {
        if (!await repository.ClassExistsAsync(owner, classId, ct)) return new(null, ClassroomFailure.NotFound);
        var range = new SessionRange(input.From, input.To);
        var errors = range.Validate();
        if (errors.Count > 0) return new(null, ClassroomFailure.Invalid, Errors: errors);
        return await repository.WithClassLockAsync<IReadOnlyList<SessionView>>(classId, async () =>
        {
            var schedules = await repository.SchedulesAsync(owner, classId, ct);
            if (schedules.Count == 0 || schedules[^1].Version != input.ScheduleVersion) return new(null, ClassroomFailure.Conflict, "Lịch tuần chưa có hoặc đã thay đổi. Vui lòng tải lại lịch.");
            var sessions = (await repository.SessionsAsync(owner, classId, range, ct)).ToList();
            var created = new List<SessionView>();
            for (var day = range.From.DayNumber; day <= range.To.DayNumber; day++)
            {
                var date = DateOnly.FromDayNumber(day);
                var schedule = schedules.LastOrDefault(x => x.EffectiveFrom <= date);
                foreach (var slot in schedule?.Slots.Where(x => x.DayOfWeek == (int)date.DayOfWeek) ?? [])
                {
                    if (sessions.Any(x => x.Date == date && x.StartTime == slot.StartTime && x.EndTime == slot.EndTime)) continue;
                    if (sessions.Any(x => x.Overlaps(date, slot.StartTime, slot.EndTime))) return new(null, ClassroomFailure.Conflict, "Lịch trùng với buổi đã lưu. Các buổi cũ được giữ nguyên; chưa tạo buổi nào trong lần này.");
                    var item = new ClassSession(owner, classId, date, slot.StartTime, slot.EndTime, schedule!.Version);
                    repository.Add(item); repository.Add(new SessionChange(item, "session.created", clock.GetUtcNow())); sessions.Add(item); created.Add(SessionView.From(item));
                }
            }
            await repository.SaveAsync(ct);
            return new(created);
        }, ct);
    }
    public async Task<ClassroomResult<IReadOnlyList<AttendanceView>>> AttendanceAsync(string owner, Guid classId, Guid id, CancellationToken ct)
    {
        var session = await repository.FindSessionAsync(owner, classId, id, ct);
        if (session is null) return new(null, ClassroomFailure.NotFound);
        var students = await repository.EligibleStudentsAsync(owner, classId, session.Date, ct);
        var saved = await repository.AttendanceAsync(owner, id, ct);
        return new(List(session, students, saved));
    }
    // Saves every changed mark of one session in a single transaction: a stale or unknown row rejects the whole batch.
    public async Task<ClassroomResult<IReadOnlyList<AttendanceView>>> MarkManyAsync(string owner, Guid classId, Guid id, AttendanceBatchInput input, CancellationToken ct) =>
        await repository.WithClassLockAsync<IReadOnlyList<AttendanceView>>(classId, async () =>
        {
            var session = await repository.FindSessionAsync(owner, classId, id, ct);
            if (session is null) return new(null, ClassroomFailure.NotFound);
            if (session.Status != "held") return new(null, ClassroomFailure.Conflict, "Chỉ điểm danh buổi đã diễn ra. Buổi dự kiến hoặc đã hủy không được điểm danh.");
            var errors = input.Validate();
            if (errors.Count > 0) return new(null, ClassroomFailure.Invalid, Errors: errors);
            var students = await repository.EligibleStudentsAsync(owner, classId, session.Date, ct);
            var saved = (await repository.AttendanceAsync(owner, id, ct)).ToList();
            var known = students.Concat(saved.Select(x => x.Student)).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
            var marks = saved.ToDictionary(x => x.Student.Id, x => x.Attendance);
            foreach (var item in input.Marks!)
            {
                if (!known.TryGetValue(item.StudentId, out var student)) return new(null, ClassroomFailure.NotFound);
                if (item.Version != (marks.GetValueOrDefault(item.StudentId)?.Version ?? 0))
                    return new(null, ClassroomFailure.Conflict, $"Điểm danh của {student.Name} đã thay đổi ở tab khác. Vui lòng tải lại; nội dung đang nhập được giữ.");
            }
            foreach (var item in input.Marks!)
            {
                var existing = marks.GetValueOrDefault(item.StudentId);
                if (Attendance.Same(existing, item.Input)) continue;
                var mark = existing ?? new Attendance(owner, id, item.StudentId);
                mark.TryMark(item.Input);
                if (existing is null) { repository.Add(mark); saved.Add(new(mark, known[item.StudentId])); }
                // One session version per changed mark keeps the outbox ordered per student (unique SessionId+SessionVersion).
                session.RecordAttendanceChange();
                repository.Add(new SessionChange(session, "attendance.changed", clock.GetUtcNow(), mark));
            }
            await repository.SaveAsync(ct);
            return new(List(session, students, saved));
        }, ct);
    public async Task<ClassroomResult<AttendanceView>> MarkAsync(string owner, Guid classId, Guid id, Guid studentId, AttendanceInput input, CancellationToken ct) =>
        await repository.WithClassLockAsync<AttendanceView>(classId, async () =>
        {
            var session = await repository.FindSessionAsync(owner, classId, id, ct);
            if (session is null) return new(null, ClassroomFailure.NotFound);
            var saved = (await repository.AttendanceAsync(owner, id, ct)).SingleOrDefault(x => x.Student.Id == studentId);
            var student = saved?.Student ?? (await repository.EligibleStudentsAsync(owner, classId, session.Date, ct)).SingleOrDefault(x => x.Id == studentId);
            if (student is null) return new(null, ClassroomFailure.NotFound);
            if (session.Status != "held") return new(null, ClassroomFailure.Conflict, "Chỉ điểm danh buổi đã diễn ra. Buổi dự kiến hoặc đã hủy không được điểm danh.");
            var errors = input.Validate();
            if (errors.Count > 0) return new(null, ClassroomFailure.Invalid, Errors: errors);
            var mark = saved?.Attendance ?? new Attendance(owner, id, studentId);
            if (!mark.TryMark(input)) return new(null, ClassroomFailure.Conflict, "Điểm danh đã thay đổi ở tab khác. Vui lòng tải lại; nội dung đang nhập được giữ.");
            if (saved is null) repository.Add(mark);
            session.RecordAttendanceChange();
            repository.Add(new SessionChange(session, "attendance.changed", clock.GetUtcNow(), mark));
            await repository.SaveAsync(ct);
            return new(View(session, student, mark));
        }, ct);
    // One read for the "Hôm nay" screen: every session of the owner on a Vietnam calendar date, with
    // per-session attendance counts, in three queries instead of one request per class.
    // Read-only and owner-scoped: only the teacher's own classes, only schedules already in effect.
    public async Task<ClassroomResult<IReadOnlyList<ClassScheduleView>>> ActiveSchedulesAsync(string owner, Guid? exceptClassId, CancellationToken ct)
    {
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(ClassSession.TimeZone)).DateTime);
        var rows = await repository.ActiveSchedulesAsync(owner, day, ct);
        return new(rows.Where(x => x.Schedule.ClassId != exceptClassId && x.Schedule.Slots.Count > 0).OrderBy(x => x.ClassName).ThenBy(x => x.Schedule.ClassId)
            .Select(x => new ClassScheduleView(x.Schedule.ClassId, x.ClassName, x.Schedule.EffectiveFrom, ScheduleView.From(x.Schedule).Slots)).ToArray());
    }
    public async Task<ClassroomResult<TodayView>> TodayAsync(string owner, DateOnly? date, CancellationToken ct)
    {
        var day = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(ClassSession.TimeZone)).DateTime);
        var sessions = await repository.SessionsOnAsync(owner, day, ct);
        var marks = (await repository.MarksAsync(owner, sessions.Select(x => x.Session.Id).ToArray(), ct)).ToLookup(x => x.SessionId);
        var eligible = (await repository.EligibleAsync(owner, sessions.Select(x => x.Session.ClassId).Distinct().ToArray(), day, ct)).ToLookup(x => x.ClassId, x => x.StudentId);
        return new(new TodayView(day, sessions.Select(x =>
        {
            var saved = marks[x.Session.Id].ToArray();
            var students = eligible[x.Session.ClassId].Concat(saved.Select(m => m.StudentId)).Distinct().Count();
            return new TodaySession(x.Session.ClassId, x.ClassName, x.Subject, SessionView.From(x.Session), students,
                saved.Count(m => m.Status == "present"), saved.Count(m => m.Status == "absent"));
        }).ToArray()));
    }
    // Counts only: attendance rate is present / (present + absent); unmarked students are not guessed.
    public async Task<ClassroomResult<MonthSummary>> MonthAsync(string owner, string month, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var first))
            return new(null, ClassroomFailure.Invalid, Errors: new() { ["month"] = ["Tháng không hợp lệ (yyyy-MM)."] });
        var sessions = await repository.StatusesAsync(owner, first, first.AddMonths(1).AddDays(-1), ct);
        var heldIds = sessions.Where(x => x.Status == "held").Select(x => x.Id).ToArray();
        var marks = await repository.MarksAsync(owner, heldIds, ct);
        var present = marks.Count(x => x.Status == "present");
        var absent = marks.Count(x => x.Status == "absent");
        return new(new MonthSummary(month, heldIds.Length, sessions.Count(x => x.Status == "planned"), sessions.Count(x => x.Status == "cancelled"),
            present, absent, present + absent == 0 ? null : (double)present / (present + absent)));
    }
    public async Task<ClassroomResult<IReadOnlyList<MissingAttendance>>> MissingAsync(string owner, Guid classId, SessionRange range, CancellationToken ct)
    {
        var sessions = await ListAsync(owner, classId, range, ct);
        if (sessions.Failure is not null) return new(null, sessions.Failure, sessions.Message, sessions.Errors);
        var missing = new List<MissingAttendance>();
        foreach (var session in sessions.Value!.Where(x => x.Status == "held"))
        {
            var attendance = await AttendanceAsync(owner, classId, session.Id, ct);
            missing.AddRange(attendance.Value!.Where(x => x.Status == "unmarked").Select(x => new MissingAttendance(session.Id, session.Date, session.StartTime, x.StudentId, x.Name)));
        }
        return new(missing);
    }
    public async Task<ClassroomResult<IReadOnlyList<ClassroomChange>>> HistoryAsync(string owner, Guid classId, Guid id, CancellationToken ct)
    {
        if (await repository.FindSessionAsync(owner, classId, id, ct) is null) return new(null, ClassroomFailure.NotFound);
        return new((await repository.ChangesAsync(owner, id, ct)).Select(ChangeView).ToArray());
    }
    public static ClassroomChange ChangeView(SessionChange x) => new(x.Id, x.OwnerId, x.ClassId, x.SessionId, x.SessionVersion, x.Kind, x.RecordedAt, x.SessionStatus, x.StudentId, x.AttendanceVersion, x.AttendanceStatus, x.Excused, x.Reason);
    private static AttendanceView[] List(ClassSession session, IEnumerable<Student> students, IReadOnlyCollection<AttendanceWithStudent> saved) =>
        students.Concat(saved.Select(x => x.Student)).DistinctBy(x => x.Id).OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(student => View(session, student, saved.SingleOrDefault(x => x.Student.Id == student.Id)?.Attendance)).ToArray();
    private static AttendanceView View(ClassSession session, Student student, Attendance? mark) => new(student.Id, student.Name, mark?.Status ?? "unmarked", mark?.Reason, mark?.Excused ?? false, mark?.Version ?? 0, session.Version);
}
