using System.Security.Claims;
using EducationKeeptrack.Classroom.Domain;
using EducationKeeptrack.Classroom.Infrastructure;
using EducationKeeptrack.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
namespace EducationKeeptrack.Classroom;

public static class RosterEndpoints
{
    public static void MapRoster(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").RequireAuthorization(policy => policy.RequireRole("Teacher"));
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
            return (HttpMethods.IsGet(http.Request.Method) ? access.CanRead : access.CanWrite)
                ? await next(context)
                : Results.Json(new { message = "Quyền sử dụng hiện tại không cho phép thao tác này.", status = access.Status }, statusCode: 403);
        });
        group.MapPost("/students", async (StudentInput input, HttpContext http, ClassroomDatabase db) =>
        {
            var errors = input.Validate();
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var student = new Student(Owner(http), input);
            db.Students.Add(student);
            await db.SaveChangesAsync(http.RequestAborted);
            return Results.Created($"/api/students/{student.Id}", StudentView(student));
        });
        group.MapGet("/students", async (HttpContext http, ClassroomDatabase db) =>
        {
            var owner = Owner(http);
            var students = await db.Students.AsNoTracking().Where(x => x.OwnerId == owner).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(http.RequestAborted);
            return Results.Ok(students.Select(StudentView));
        });
        group.MapGet("/students/{id:guid}", async (Guid id, HttpContext http, ClassroomDatabase db) =>
        {
            var owner = Owner(http);
            var student = await db.Students.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, http.RequestAborted);
            return student is null ? Results.NotFound() : Results.Ok(StudentView(student));
        });
        group.MapPut("/students/{id:guid}", async (Guid id, StudentInput input, HttpContext http, ClassroomDatabase db) =>
        {
            var owner = Owner(http);
            var student = await db.Students.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, http.RequestAborted);
            if (student is null) return Results.NotFound();
            var errors = input.Validate();
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            student.Update(input);
            await db.SaveChangesAsync(http.RequestAborted);
            return Results.Ok(StudentView(student));
        });
        group.MapPost("/classes/{classId:guid}/enrollments", async (Guid classId, EnrollmentInput input, HttpContext http, ClassroomDatabase db, TimeProvider clock) =>
        {
            var owner = Owner(http);
            if (!await db.Classes.AnyAsync(x => x.Id == classId && x.OwnerId == owner, http.RequestAborted)
                || !await db.Students.AnyAsync(x => x.Id == input.StudentId && x.OwnerId == owner, http.RequestAborted)) return Results.NotFound();
            if (input.StartDate == default) return Results.ValidationProblem(new Dictionary<string, string[]> { ["startDate"] = ["Vui lòng nhập ngày bắt đầu."] });
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({input.StudentId.ToString()}, 0))", http.RequestAborted);
            if (await db.Enrollments.AnyAsync(x => x.OwnerId == owner && x.ClassId == classId && x.StudentId == input.StudentId && (x.EndDate == null || x.EndDate >= input.StartDate), http.RequestAborted))
                return Results.Conflict(new { message = "Học sinh đã có đăng ký đang hiệu lực hoặc trùng thời gian trong lớp này." });
            var enrollment = new Enrollment(owner, classId, input.StudentId, input.StartDate, clock.GetUtcNow());
            db.Enrollments.Add(enrollment);
            try { await db.SaveChangesAsync(http.RequestAborted); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return Results.Conflict(new { message = "Học sinh đã có đăng ký đang hiệu lực trong lớp này." });
            }
            await transaction.CommitAsync(http.RequestAborted);
            var student = await db.Students.SingleAsync(x => x.Id == input.StudentId && x.OwnerId == owner, http.RequestAborted);
            return Results.Created($"/api/classes/{classId}/enrollments/{enrollment.Id}", EnrollmentView(enrollment, student));
        });
        group.MapPut("/classes/{classId:guid}/enrollments/{id:guid}", async (Guid classId, Guid id, EnrollmentStatusInput input, HttpContext http, ClassroomDatabase db, TimeProvider clock) =>
        {
            var owner = Owner(http);
            var item = await db.Enrollments.Include(x => x.History).SingleOrDefaultAsync(x => x.Id == id && x.ClassId == classId && x.OwnerId == owner, http.RequestAborted);
            if (item is null) return Results.NotFound();
            if (item.Version != input.Version || item.Status == "stopped") return Results.Conflict(new { message = "Đăng ký đã thay đổi hoặc đã dừng học. Vui lòng tải lại danh sách; tạo đăng ký mới nếu học lại." });
            var errors = item.ValidateChange(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (item.Status == input.Status) return Results.Conflict(new { message = "Đăng ký đang ở tình trạng này." });
            item.Change(input, clock.GetUtcNow());
            try { await db.SaveChangesAsync(http.RequestAborted); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "Đăng ký đã được thay đổi. Vui lòng tải lại danh sách." }); }
            var student = await db.Students.SingleAsync(x => x.Id == item.StudentId && x.OwnerId == owner, http.RequestAborted);
            return Results.Ok(EnrollmentView(item, student));
        });
        group.MapGet("/classes/{classId:guid}/enrollments", async (Guid classId, HttpContext http, ClassroomDatabase db) =>
        {
            var owner = Owner(http);
            if (!await db.Classes.AnyAsync(x => x.Id == classId && x.OwnerId == owner, http.RequestAborted)) return Results.NotFound();
            var rows = await (from enrollment in db.Enrollments.AsNoTracking().Include(x => x.History)
                              join student in db.Students.AsNoTracking() on enrollment.StudentId equals student.Id
                              where enrollment.ClassId == classId && enrollment.OwnerId == owner && student.OwnerId == owner
                              orderby student.Name, enrollment.StartDate
                              select new { Enrollment = enrollment, Student = student }).ToListAsync(http.RequestAborted);
            return Results.Ok(rows.Select(x => EnrollmentView(x.Enrollment, x.Student)));
        });
    }
    private static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static object StudentView(Student student) => new { student.Id, student.Name, student.ParentName, student.Phone, student.Email, student.Notes };
    private static object EnrollmentView(Enrollment item, Student student) => new { item.Id, item.StudentId, item.StartDate, item.EndDate, item.Status, item.Version, Student = StudentView(student), History = item.History.OrderBy(x => x.Version).Select(x => new { x.Status, x.EffectiveDate, x.RecordedAt, x.Version }) };
}
