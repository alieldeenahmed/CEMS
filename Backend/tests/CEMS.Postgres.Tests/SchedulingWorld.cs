using CEMS.Application.Common.Interfaces;
using CEMS.Application.Scheduling;
using CEMS.Application.Scheduling.Commands.CreateSession;
using CEMS.Application.Scheduling.Commands.SubstituteSessionTeacher;
using CEMS.Domain.Branches;
using CEMS.Domain.Courses;
using CEMS.Domain.Teachers;
using CEMS.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Postgres.Tests;

public sealed class FakeUser : ICurrentUserService
{
    public Guid? UserId { get; init; } = Guid.NewGuid();
    public IReadOnlyList<string> Roles { get; init; } = new[] { RoleNames.Owner };
    public IReadOnlyList<Guid> BranchIds { get; init; } = Array.Empty<Guid>();

    public bool IsInRole(string role) => Roles.Contains(role);
    public bool HasAccessToBranch(Guid branchId) => IsInRole(RoleNames.Owner) || BranchIds.Contains(branchId);
}

/// <summary>Result of one concurrent attempt: it either produced a value or threw.</summary>
public sealed record Outcome<T>(T? Value, Exception? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>
/// A branch with a course, several rooms, and several teachers who are qualified for the course and
/// available every Monday 09:00-17:00, in a fresh PostgreSQL database. Handlers are run the way the API
/// runs them: each call gets its own DbContext (its own connection and transaction), so calls made
/// together genuinely race in the database.
/// </summary>
public sealed class SchedulingWorld : IAsyncDisposable
{
    /// <summary>A Monday, well in the future so "already started" rules never apply.</summary>
    public static readonly DateTime Slot = new(2030, 1, 7, 10, 0, 0, DateTimeKind.Utc);

    private SchedulingWorld(PostgresTestDatabase database) => Database = database;

    public PostgresTestDatabase Database { get; }
    public Guid BranchId { get; private set; }
    public Guid CourseId { get; private set; }
    public List<Guid> RoomIds { get; } = new();
    public List<Guid> TeacherIds { get; } = new();

    public static async Task<SchedulingWorld> CreateAsync(int rooms, int teachers)
    {
        var world = new SchedulingWorld(await PostgresTestDatabase.CreateAsync());
        await using var context = world.Database.CreateContext();

        var branch = new Branch { Id = Guid.NewGuid(), Name = "Smouha", Address = "14 Fawzy Moaz St", Phone = "034567001" };
        var curriculum = new Curriculum { Id = Guid.NewGuid(), Name = "Web & Software", Description = "Python and web" };
        var course = new Course { Id = Guid.NewGuid(), Name = "Python Fundamentals", DeliveryMode = DeliveryMode.Group, CurriculumId = curriculum.Id, BranchId = branch.Id };
        context.AddRange(branch, curriculum, course);

        for (var i = 0; i < rooms; i++)
        {
            var room = new Room { Id = Guid.NewGuid(), Name = $"Lab {i + 1}", Capacity = 15, BranchId = branch.Id };
            context.Rooms.Add(room);
            world.RoomIds.Add(room.Id);
        }

        for (var i = 0; i < teachers; i++)
        {
            var teacher = new Teacher { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), HireDate = new DateOnly(2024, 1, 1), PayType = PayType.Hourly, PayRate = 100 };
            context.Teachers.Add(teacher);
            context.TeacherBranches.Add(new TeacherBranch { TeacherId = teacher.Id, BranchId = branch.Id });
            context.TeacherCourseQualifications.Add(new TeacherCourseQualification { TeacherId = teacher.Id, CourseId = course.Id });
            context.TeacherAvailabilities.Add(new TeacherAvailability
            {
                Id = Guid.NewGuid(), TeacherId = teacher.Id, BranchId = branch.Id, DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0)
            });
            world.TeacherIds.Add(teacher.Id);
        }

        await context.SaveChangesAsync();
        world.BranchId = branch.Id;
        world.CourseId = course.Id;
        return world;
    }

    public async Task<CourseSessionDto> CreateSessionAsync(
        Guid roomId, Guid teacherId, DateTime start, TimeSpan? length = null, bool @override = false, string? reason = null, FakeUser? user = null)
    {
        await using var context = Database.CreateContext();
        var handler = new CreateSessionCommandHandler(context, user ?? new FakeUser());
        return await handler.Handle(
            new CreateSessionCommand(CourseId, roomId, teacherId, start, start + (length ?? TimeSpan.FromHours(1)), @override, reason),
            CancellationToken.None);
    }

    public async Task<CourseSessionDto> SubstituteAsync(Guid sessionId, Guid newTeacherId, bool @override = false, string? reason = null)
    {
        await using var context = Database.CreateContext();
        var handler = new SubstituteSessionTeacherCommandHandler(context, new FakeUser());
        return await handler.Handle(new SubstituteSessionTeacherCommand(sessionId, newTeacherId, @override, reason), CancellationToken.None);
    }

    /// <summary>Inserts a session directly, bypassing every handler rule (but not the database's).</summary>
    public async Task<Guid> InsertSessionAsync(
        Guid roomId, Guid teacherId, DateTime start, DateTime? end = null, bool overridden = false, SessionStatus status = SessionStatus.Scheduled)
    {
        await using var context = Database.CreateContext();
        var session = NewSession(roomId, teacherId, start, end, overridden, status);
        context.CourseSessions.Add(session);
        await context.SaveChangesAsync();
        return session.Id;
    }

    public CourseSession NewSession(Guid roomId, Guid teacherId, DateTime start, DateTime? end = null, bool overridden = false, SessionStatus status = SessionStatus.Scheduled) => new()
    {
        Id = Guid.NewGuid(), CourseId = CourseId, RoomId = roomId, TeacherId = teacherId,
        StartUtc = start, EndUtc = end ?? start.AddHours(1), Status = status,
        Overridden = overridden, OverrideReason = overridden ? "test override" : null
    };

    public async Task<List<CourseSession>> SessionsAsync()
    {
        await using var context = Database.CreateContext();
        return await context.CourseSessions.AsNoTracking().ToListAsync();
    }

    /// <summary>
    /// The invariant the whole design exists to protect: two live sessions that overlap in the same room,
    /// or for the same teacher, are only ever allowed if a manager knowingly overrode one of them.
    /// </summary>
    public static void AssertNoUnauthorisedOverlaps(IReadOnlyList<CourseSession> sessions)
    {
        var live = sessions.Where(s => s.Status != SessionStatus.Cancelled).ToList();
        for (var i = 0; i < live.Count; i++)
        {
            for (var j = i + 1; j < live.Count; j++)
            {
                var a = live[i];
                var b = live[j];
                var overlap = a.StartUtc < b.EndUtc && b.StartUtc < a.EndUtc;
                var shared = a.RoomId == b.RoomId || a.TeacherId == b.TeacherId;
                if (overlap && shared)
                {
                    Assert.True(a.Overridden || b.Overridden,
                        $"Sessions {a.Id} and {b.Id} overlap in a shared room/teacher but neither was overridden.");
                }
            }
        }
    }

    /// <summary>Starts every action at the same moment, each on its own thread, and collects what happened.</summary>
    public static async Task<List<Outcome<T>>> RaceAsync<T>(int count, Func<int, Task<T>> action)
    {
        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, count).Select(i => Task.Run(async () =>
        {
            await gate.Task;
            try
            {
                return new Outcome<T>(await action(i), null);
            }
            catch (Exception ex)
            {
                return new Outcome<T>(default, ex);
            }
        })).ToList();

        await Task.Delay(200);   // let every task reach the gate
        gate.SetResult();
        return (await Task.WhenAll(tasks)).ToList();
    }

    public ValueTask DisposeAsync() => Database.DisposeAsync();
}
