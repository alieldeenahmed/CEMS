using CEMS.Application.Common.Exceptions;
using CEMS.Domain.Courses;
using CEMS.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Postgres.Tests;

/// <summary>
/// Requests for the same room or teacher arriving at the same instant. Each attempt runs the real
/// handler with its own DbContext against PostgreSQL, released together by a gate, so the "check, then
/// insert" window is genuinely contended. Without the locks the handlers take (and, behind them, the
/// exclusion constraints) several of these would all succeed.
/// </summary>
public class ConcurrentSchedulingTests
{
    private const int Parallel = 8;
    private static readonly DateTime Slot = SchedulingWorld.Slot;

    private static void AssertOnlyConflictsFailed<T>(IEnumerable<Outcome<T>> outcomes)
    {
        foreach (var failure in outcomes.Where(o => !o.Succeeded))
        {
            // Anything else -- a deadlock, a raw PostgresException, a timeout -- is a bug, not "a conflict".
            Assert.IsType<SchedulingConflictException>(failure.Error);
        }
    }

    [Fact]
    public async Task EightRequestsForTheSameRoomAndTime_ExactlyOneWins()
    {
        await using var world = await SchedulingWorld.CreateAsync(rooms: 1, teachers: Parallel);

        var outcomes = await SchedulingWorld.RaceAsync(Parallel, i =>
            world.CreateSessionAsync(world.RoomIds[0], world.TeacherIds[i], Slot));

        Assert.Equal(1, outcomes.Count(o => o.Succeeded));
        AssertOnlyConflictsFailed(outcomes);
        var stored = await world.SessionsAsync();
        Assert.Single(stored);
        Assert.False(stored[0].Overridden);
    }

    [Fact]
    public async Task EightRequestsForTheSameTeacherAndTime_ExactlyOneWins()
    {
        await using var world = await SchedulingWorld.CreateAsync(rooms: Parallel, teachers: 1);

        var outcomes = await SchedulingWorld.RaceAsync(Parallel, i =>
            world.CreateSessionAsync(world.RoomIds[i], world.TeacherIds[0], Slot));

        Assert.Equal(1, outcomes.Count(o => o.Succeeded));
        AssertOnlyConflictsFailed(outcomes);
        Assert.Single(await world.SessionsAsync());
    }

    [Fact]
    public async Task PartiallyOverlappingRequests_NeverProduceAnOverlap()
    {
        await using var world = await SchedulingWorld.CreateAsync(rooms: 1, teachers: 6);

        // Six one-hour requests staggered 20 minutes apart in the same room: many overlap each other.
        var outcomes = await SchedulingWorld.RaceAsync(6, i =>
            world.CreateSessionAsync(world.RoomIds[0], world.TeacherIds[i], Slot.AddMinutes(20 * i)));

        AssertOnlyConflictsFailed(outcomes);
        var stored = await world.SessionsAsync();
        Assert.Equal(outcomes.Count(o => o.Succeeded), stored.Count);
        Assert.InRange(stored.Count, 1, 2);   // at most two of these can coexist in one room within the window
        SchedulingWorld.AssertNoUnauthorisedOverlaps(stored);
    }

    [Fact]
    public async Task IndependentRequests_AllSucceedTogether_WithoutDeadlockingOrSerialisingIntoFailure()
    {
        await using var world = await SchedulingWorld.CreateAsync(rooms: Parallel, teachers: Parallel);

        var outcomes = await SchedulingWorld.RaceAsync(Parallel, i =>
            world.CreateSessionAsync(world.RoomIds[i], world.TeacherIds[i], Slot));

        Assert.All(outcomes, o => Assert.True(o.Succeeded, o.Error?.ToString()));
        Assert.Equal(Parallel, (await world.SessionsAsync()).Count);
    }

    [Fact]
    public async Task RequestsSharingRoomsAndTeachersInCrossingPatterns_DoNotDeadlock_AndKeepTheInvariant()
    {
        await using var world = await SchedulingWorld.CreateAsync(rooms: 2, teachers: 3);

        // 12 requests over 2 rooms x 3 teachers, every one at the same slot. Each locks a (room, teacher)
        // pair; locks are taken in a fixed order, so crossing pairs cannot wait on each other in a cycle.
        var outcomes = await SchedulingWorld.RaceAsync(12, i =>
            world.CreateSessionAsync(world.RoomIds[i % 2], world.TeacherIds[i % 3], Slot));

        AssertOnlyConflictsFailed(outcomes);
        var stored = await world.SessionsAsync();
        Assert.InRange(stored.Count, 1, 2);   // one session per room at most
        Assert.Equal(outcomes.Count(o => o.Succeeded), stored.Count);
        SchedulingWorld.AssertNoUnauthorisedOverlaps(stored);
    }

    [Fact]
    public async Task ConcurrentOverrides_AllSucceed_ButOnlyTheFirstIsOrdinary()
    {
        await using var world = await SchedulingWorld.CreateAsync(rooms: 1, teachers: 5);
        var manager = new FakeUser { Roles = new[] { RoleNames.BranchManager }, BranchIds = new[] { world.BranchId } };

        // Overriding is a deliberate, recorded decision, so it must keep working under concurrency: the
        // winner of the race is an ordinary booking and everyone else is a recorded override.
        var outcomes = await SchedulingWorld.RaceAsync(5, i =>
            world.CreateSessionAsync(world.RoomIds[0], world.TeacherIds[i], Slot, @override: true, reason: "Open day", user: manager));

        Assert.All(outcomes, o => Assert.True(o.Succeeded, o.Error?.ToString()));
        var stored = await world.SessionsAsync();
        Assert.Equal(5, stored.Count);
        Assert.Equal(1, stored.Count(s => !s.Overridden));
        Assert.All(stored.Where(s => s.Overridden), s => Assert.Equal("Open day", s.OverrideReason));
    }

    [Fact]
    public async Task TwoSubstitutionsOfTheSameTeacherIntoOverlappingSessions_ExactlyOneWins()
    {
        await using var world = await SchedulingWorld.CreateAsync(rooms: 2, teachers: 3);
        var first = await world.CreateSessionAsync(world.RoomIds[0], world.TeacherIds[0], Slot);
        var second = await world.CreateSessionAsync(world.RoomIds[1], world.TeacherIds[1], Slot.AddMinutes(30));
        var cover = world.TeacherIds[2];

        var outcomes = await SchedulingWorld.RaceAsync(2, i => world.SubstituteAsync(i == 0 ? first.Id : second.Id, cover));

        Assert.Equal(1, outcomes.Count(o => o.Succeeded));
        AssertOnlyConflictsFailed(outcomes);
        var stored = await world.SessionsAsync();
        Assert.Single(stored, s => s.TeacherId == cover);
        SchedulingWorld.AssertNoUnauthorisedOverlaps(stored);
    }

    [Fact]
    public async Task ASubstitutionRacingANewBookingForTheSameTeacher_ExactlyOneWins()
    {
        await using var world = await SchedulingWorld.CreateAsync(rooms: 2, teachers: 3);
        var existing = await world.CreateSessionAsync(world.RoomIds[0], world.TeacherIds[0], Slot);
        var cover = world.TeacherIds[2];

        var outcomes = await SchedulingWorld.RaceAsync(2, i => i == 0
            ? world.SubstituteAsync(existing.Id, cover)
            : world.CreateSessionAsync(world.RoomIds[1], cover, Slot));

        Assert.Equal(1, outcomes.Count(o => o.Succeeded));
        AssertOnlyConflictsFailed(outcomes);
        SchedulingWorld.AssertNoUnauthorisedOverlaps(await world.SessionsAsync());
    }

    [Fact]
    public async Task ACancellationRacingANewBooking_LeavesAConsistentTimetable()
    {
        await using var world = await SchedulingWorld.CreateAsync(rooms: 1, teachers: 2);
        var existing = await world.CreateSessionAsync(world.RoomIds[0], world.TeacherIds[0], Slot);

        // Whichever order they interleave in, the result must satisfy the invariant: either the new booking
        // is refused (cancel had not happened yet) or it lands after the slot was freed.
        var outcomes = await SchedulingWorld.RaceAsync(2, async i =>
        {
            if (i == 0)
            {
                await using var context = world.Database.CreateContext();
                var session = await context.CourseSessions.SingleAsync(s => s.Id == existing.Id);
                session.Status = SessionStatus.Cancelled;
                await context.SaveChangesAsync();
                return existing.Id;
            }

            return (await world.CreateSessionAsync(world.RoomIds[0], world.TeacherIds[1], Slot)).Id;
        });

        AssertOnlyConflictsFailed(outcomes);
        SchedulingWorld.AssertNoUnauthorisedOverlaps(await world.SessionsAsync());
    }

    [Fact]
    public async Task AFailedCreate_LeavesNothingBehind()
    {
        await using var world = await SchedulingWorld.CreateAsync(rooms: 1, teachers: 1);
        await world.CreateSessionAsync(world.RoomIds[0], world.TeacherIds[0], Slot);

        await Assert.ThrowsAsync<SchedulingConflictException>(() =>
            world.CreateSessionAsync(world.RoomIds[0], world.TeacherIds[0], Slot));

        Assert.Single(await world.SessionsAsync());
    }
}
