using System.Globalization;
using EducationKeeptrack.Billing.Domain;
using EducationKeeptrack.Contracts;
namespace EducationKeeptrack.Billing.Application;

public enum BillingFailure { NotFound, Invalid, Conflict }
public record BillingResult<T>(T? Value, BillingFailure? Failure = null, string? Message = null, Dictionary<string, string[]>? Errors = null) where T : class;
public record RateView(Guid Id, DateOnly EffectiveFrom, string UnitPriceVnd, int Version);
public record RatePlanView(int Version, IReadOnlyList<RateView> Rates);
public record TuitionLine(Guid SessionId, DateOnly Date, TimeOnly StartTime, string Kind, string SessionStatus, string AttendanceStatus, string? UnitPriceVnd, string? AmountVnd, Guid? RateId, int SourceVersion);
public record StudentTuition(Guid StudentId, string Name, string TotalVnd, IReadOnlyList<TuitionLine> Lines);
public record TuitionReport(string Month, long Version, int RateVersion, string Status, string TotalVnd, IReadOnlyList<StudentTuition> Students);
public record BillingSnapshot(TuitionLedger? Ledger, IReadOnlyList<SessionCharge> Sessions);
public record ProjectionComplete;

public interface IBillingRepository
{
    Task<TuitionLedger?> LedgerAsync(string owner, Guid classId, CancellationToken ct);
    Task<IReadOnlyList<SessionCharge>> ChargesAsync(string owner, Guid classId, CancellationToken ct);
    Task<bool> ProcessedAsync(Guid eventId, CancellationToken ct);
    Task<BillingSnapshot> SnapshotAsync(string owner, Guid classId, CancellationToken ct);
    void Add(TuitionLedger ledger);
    void Add(SessionCharge charge);
    void Add(BillingInbox inbox);
    void Add(BillingOutbox change);
    Task SaveAsync(CancellationToken ct);
    Task<BillingResult<T>> WithClassLockAsync<T>(Guid classId, Func<Task<BillingResult<T>>> action, CancellationToken ct) where T : class;
}

public sealed class BillingService(IBillingRepository repository, IClassroomBillingSource classroom, TimeProvider clock)
{
    public async Task<BillingResult<RatePlanView>> RatesAsync(string owner, Guid classId, CancellationToken ct)
    {
        if (await classroom.ReadClassAsync(owner, classId, ct) is null) return new(null, BillingFailure.NotFound);
        return new(RatesView(await repository.LedgerAsync(owner, classId, ct)));
    }
    public async Task<BillingResult<RatePlanView>> AddRateAsync(string owner, Guid classId, RateInput input, CancellationToken ct)
    {
        if (await classroom.ReadClassAsync(owner, classId, ct) is null) return new(null, BillingFailure.NotFound);
        var errors = input.Validate();
        if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
        return await repository.WithClassLockAsync<RatePlanView>(classId, async () =>
        {
            var ledger = await repository.LedgerAsync(owner, classId, ct);
            if (input.Version != (ledger?.RateVersion ?? 0)) return new(null, BillingFailure.Conflict, "Đơn giá đã thay đổi. Vui lòng tải lại trước khi lưu tiếp.");
            if (ledger?.Rates.Any(x => x.EffectiveFrom == input.EffectiveFrom) == true) return new(null, BillingFailure.Conflict, "Ngày hiệu lực đã có đơn giá. Không ghi đè đơn giá đã lưu.");
            if (ledger is null) { ledger = new TuitionLedger(owner, classId); repository.Add(ledger); }
            // Freeze already-held sessions using the existing plan even if event delivery is delayed.
            var source = await classroom.ReadClassAsync(owner, classId, ct);
            if (source is null) return new(null, BillingFailure.NotFound);
            var charges = await repository.ChargesAsync(owner, classId, ct);
            var nextDate = ledger.Rates.Where(x => x.EffectiveFrom > input.EffectiveFrom).OrderBy(x => x.EffectiveFrom).Select(x => (DateOnly?)x.EffectiveFrom).FirstOrDefault();
            bool Affected(DateOnly date) => date >= input.EffectiveFrom && (nextDate is null || date < nextDate);
            if (charges.Any(x => x.UnitPriceVnd is not null && Affected(x.Date))
                || source.Sessions.Any(x => x.Status == "held" && ledger.RateOn(x.Date) is not null && Affected(x.Date)))
                return new(null, BillingFailure.Conflict, "Đơn giá mới sẽ thay đổi căn cứ của buổi đã diễn ra. Vui lòng chọn ngày hiệu lực sau các buổi đó; điều chỉnh lịch sử phải thực hiện riêng có lý do.");
            foreach (var session in source.Sessions) Apply(ledger, charges.SingleOrDefault(x => x.SessionId == session.Id), session);
            ledger.AddRate(input, clock.GetUtcNow());
            repository.Add(new BillingOutbox(ledger, "rate.changed", clock.GetUtcNow()));
            await repository.SaveAsync(ct);
            return new(RatesView(ledger));
        }, ct);
    }
    public async Task ApplyEventAsync(ClassroomChange change, CancellationToken ct)
    {
        var source = await classroom.ReadSessionAsync(change.OwnerId, change.ClassId, change.SessionId, ct);
        if (source is null) return;
        await repository.WithClassLockAsync<ProjectionComplete>(change.ClassId, async () =>
        {
            if (await repository.ProcessedAsync(change.EventId, ct)) return new(new ProjectionComplete());
            var ledger = await GetOrCreateAsync(change.OwnerId, change.ClassId, ct);
            var charges = await repository.ChargesAsync(change.OwnerId, change.ClassId, ct);
            Apply(ledger, charges.SingleOrDefault(x => x.SessionId == source.Id), source);
            repository.Add(new BillingInbox(change.EventId, clock.GetUtcNow()));
            await repository.SaveAsync(ct);
            return new(new ProjectionComplete());
        }, ct);
    }
    public async Task RefreshAsync(string owner, Guid classId, CancellationToken ct)
    {
        var source = await classroom.ReadClassAsync(owner, classId, ct);
        if (source is null) return;
        await repository.WithClassLockAsync<ProjectionComplete>(classId, async () =>
        {
            var ledger = await GetOrCreateAsync(owner, classId, ct);
            var charges = await repository.ChargesAsync(owner, classId, ct);
            foreach (var session in source.Sessions) Apply(ledger, charges.SingleOrDefault(x => x.SessionId == session.Id), session);
            ledger.Refreshed(); await repository.SaveAsync(ct);
            return new(new ProjectionComplete());
        }, ct);
    }
    private async Task<TuitionLedger> GetOrCreateAsync(string owner, Guid classId, CancellationToken ct)
    {
        var ledger = await repository.LedgerAsync(owner, classId, ct);
        if (ledger is null) { ledger = new TuitionLedger(owner, classId); repository.Add(ledger); }
        return ledger;
    }
    private void Apply(TuitionLedger ledger, SessionCharge? charge, BillingSessionSource source)
    {
        var isNew = charge is null;
        charge ??= new SessionCharge(ledger.OwnerId, ledger.ClassId, source.Id);
        if (!charge.Apply(source, ledger.RateOn(source.Date))) return;
        if (isNew) repository.Add(charge);
        ledger.RecordProjection(); repository.Add(new BillingOutbox(ledger, "tuition.changed", clock.GetUtcNow()));
    }
    public async Task<BillingResult<TuitionReport>> ReportAsync(string owner, Guid classId, string month, CancellationToken ct)
    {
        var source = await classroom.ReadClassAsync(owner, classId, ct);
        if (source is null) return new(null, BillingFailure.NotFound);
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first))
            return new(null, BillingFailure.Invalid, Errors: new() { ["month"] = ["Tháng không hợp lệ. Vui lòng chọn theo dạng năm-tháng."] });
        var last = new DateOnly(first.Year, first.Month, DateTime.DaysInMonth(first.Year, first.Month));
        var snapshot = await repository.SnapshotAsync(owner, classId, ct);
        var sessions = source.Sessions.Where(x => x.Date >= first && x.Date <= last).ToArray();
        var pending = snapshot.Ledger is not null && (snapshot.Ledger.NeedsRefresh && sessions.Length > 0 || sessions.Any(s => snapshot.Sessions.SingleOrDefault(x => x.SessionId == s.Id)?.SourceVersion != s.Version));
        var rows = source.Students.Select(student =>
        {
            var lines = sessions.Where(s => s.Attendance.Any(x => x.StudentId == student.Id)).Select(session =>
            {
                var saved = snapshot.Sessions.SingleOrDefault(x => x.SessionId == session.Id);
                var fee = saved?.Fees.SingleOrDefault(x => x.StudentId == student.Id);
                var mark = session.Attendance.Single(x => x.StudentId == student.Id);
                var status = saved?.Status ?? session.Status;
                var attendance = fee?.AttendanceStatus ?? (saved is null ? mark.Status : "unmarked");
                var amount = fee?.AmountVnd ?? (status == "held" && attendance == "present" ? (decimal?)null : 0m);
                return new TuitionLine(session.Id, session.Date, session.StartTime, session.Kind, status, attendance, Money(saved?.UnitPriceVnd), Money(amount), saved?.RateId, saved?.SourceVersion ?? 0);
            }).ToArray();
            return new StudentTuition(student.Id, student.Name, Money(lines.Sum(x => x.AmountVnd is null ? 0m : decimal.Parse(x.AmountVnd, CultureInfo.InvariantCulture)))!, lines);
        }).ToArray();
        var missingRate = rows.Any(x => x.Lines.Any(line => line.AmountVnd is null));
        return new(new TuitionReport(month, snapshot.Ledger?.Version ?? 0, snapshot.Ledger?.RateVersion ?? 0, pending ? "pending" : missingRate ? "missing-rate" : "ready", Money(rows.Sum(x => decimal.Parse(x.TotalVnd, CultureInfo.InvariantCulture)))!, rows));
    }
    private static RatePlanView RatesView(TuitionLedger? ledger) => new(ledger?.RateVersion ?? 0, ledger?.Rates.OrderBy(x => x.EffectiveFrom).Select(x => new RateView(x.Id, x.EffectiveFrom, Money(x.UnitPriceVnd)!, x.Version)).ToArray() ?? []);
    private static string? Money(decimal? value) => value?.ToString("0", CultureInfo.InvariantCulture);
}
public sealed class BillingClassroomHandler(BillingService billing) : IClassroomChangeHandler
{
    public Task HandleAsync(ClassroomChange change, CancellationToken cancellationToken) => billing.ApplyEventAsync(change, cancellationToken);
}
