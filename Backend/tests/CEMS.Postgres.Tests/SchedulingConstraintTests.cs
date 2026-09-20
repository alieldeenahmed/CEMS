using CEMS.Application.Common.Exceptions;
using CEMS.Domain.Courses;
using Microsoft.EntityFrameworkCore;

namespace CEMS.Postgres.Tests;

/// <summary>
/// The database's own refusal to store an overlapping booking -- the backstop behind the handler-level
/// locks. Rows are inserted directly, so none of the handler rules run: only PostgreSQL stands between the
/// insert and the table.
/// </summary>
public class SchedulingConstraintTests : IAsyncLifetime
{
    private SchedulingWorld _world = null!;
    private Guid Room1 => _world.RoomIds[0];
    private Guid Room2 => _world.RoomIds[1];
    private Guid Teacher1 => _world.TeacherIds[0];
    private Guid Teacher2 => _world.TeacherIds[1];

    public async Task InitializeAsync() => _world = await SchedulingWorld.CreateAsync(rooms: 2, teachers: 2);
    public async Task DisposeAsync() => await _world.DisposeAsync();

    private static readonly DateTime Slot = SchedulingWorld.Slot;

    [Fact]
    public async Task TwoOrdinarySessionsInTheSameRoomCannotOverlap()
    {
        await _world.InsertSessionAsync(Room1, Teacher1, Slot);

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(() =>
            _world.InsertSessionAsync(Room1, Teacher2, Slot.AddMinutes(30)));

        Assert.Contains("room", ex.Conflicts.Single());
        Assert.Single(await _world.SessionsAsync());
    }

    [Fact]
    public async Task TwoOrdinarySessionsForTheSameTeacherCannotOverlap()
    {
        await _world.InsertSessionAsync(Room1, Teacher1, Slot);

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(() =>
            _world.InsertSessionAsync(Room2, Teacher1, Slot.AddMinutes(-30)));

        Assert.Contains("teacher", ex.Conflicts.Single());
    }

    [Fact]
    public async Task ContainmentAndIdenticalTimesAreOverlapsToo()
    {
        await _world.InsertSessionAsync(Room1, Teacher1, Slot, Slot.AddHours(3));

        await Assert.ThrowsAsync<SchedulingConflictException>(() => _world.InsertSessionAsync(Room1, Teacher2, Slot.AddHours(1), Slot.AddHours(2)));
        await Assert.ThrowsAsync<SchedulingConflictException>(() => _world.InsertSessionAsync(Room1, Teacher2, Slot, Slot.AddHours(3)));
    }

    [Fact]
    public async Task BackToBackSessionsAreNotAnOverlap()
    {
        await _world.InsertSessionAsync(Room1, Teacher1, Slot);
        await _world.InsertSessionAsync(Room1, Teacher1, Slot.AddHours(1));   // starts exactly when the first ends
        await _world.InsertSessionAsync(Room1, Teacher1, Slot.AddHours(-1));  // ends exactly when the first starts

        Assert.Equal(3, (await _world.SessionsAsync()).Count);
    }

    [Fact]
    public async Task DifferentRoomsAndTeachersAtTheSameTimeAreFine()
    {
        await _world.InsertSessionAsync(Room1, Teacher1, Slot);
        await _world.InsertSessionAsync(Room2, Teacher2, Slot);

        Assert.Equal(2, (await _world.SessionsAsync()).Count);
    }

    [Fact]
    public async Task ACancelledSessionFreesItsRoomAndTeacher()
    {
        await _world.InsertSessionAsync(Room1, Teacher1, Slot, status: SessionStatus.Cancelled);

        await _world.InsertSessionAsync(Room1, Teacher1, Slot);

        Assert.Equal(2, (await _world.SessionsAsync()).Count);
    }

    [Fact]
    public async Task AnOverriddenSessionMayKnowinglyDoubleBook_ButAnOrdinaryOneStillMayNot()
    {
        await _world.InsertSessionAsync(Room1, Teacher1, Slot);
        await _world.InsertSessionAsync(Room1, Teacher1, Slot, overridden: true);   // a manager's recorded decision

        Assert.Equal(2, (await _world.SessionsAsync()).Count);
        // The exemption belongs to the overridden row, not to the slot: an ordinary session is still refused
        // against the ordinary one.
        await Assert.ThrowsAsync<SchedulingConflictException>(() => _world.InsertSessionAsync(Room1, Teacher2, Slot));
    }

    [Fact]
    public async Task ASessionCannotEndBeforeItStarts()
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _world.InsertSessionAsync(Room1, Teacher1, Slot, Slot.AddHours(-1)));

        Assert.Contains("ck_course_sessions_end_after_start", ex.InnerException?.Message);
    }

    [Fact]
    public async Task ASessionWithZeroLengthIsRejectedToo()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => _world.InsertSessionAsync(Room1, Teacher1, Slot, Slot));
    }

    [Fact]
    public async Task ReassigningASessionOntoABusyTeacherIsRefusedByTheDatabase()
    {
        await _world.InsertSessionAsync(Room1, Teacher1, Slot);
        var second = await _world.InsertSessionAsync(Room2, Teacher2, Slot);

        await using var context = _world.Database.CreateContext();
        var session = await context.CourseSessions.SingleAsync(s => s.Id == second);
        session.TeacherId = Teacher1;

        await Assert.ThrowsAsync<SchedulingConflictException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task TheSecondOfTwoConcurrentInserts_WaitsForTheFirst_ThenIsRefused()
    {
        // No handler, no advisory lock: this is the constraint alone resolving a true race.
        await using var first = _world.Database.CreateContext();
        await using var second = _world.Database.CreateContext();

        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        first.CourseSessions.Add(_world.NewSession(Room1, Teacher1, Slot));
        await first.SaveChangesAsync();   // written but not yet committed

        second.CourseSessions.Add(_world.NewSession(Room1, Teacher2, Slot.AddMinutes(15)));
        var secondInsert = second.SaveChangesAsync();

        await Task.Delay(500);
        Assert.False(secondInsert.IsCompleted, "The second insert should be blocked until the first transaction ends.");

        await firstTransaction.CommitAsync();

        await Assert.ThrowsAsync<SchedulingConflictException>(() => secondInsert);
        Assert.Single(await _world.SessionsAsync());
    }

    [Fact]
    public async Task IfTheFirstConcurrentInsertRollsBack_TheSecondSucceeds()
    {
        await using var first = _world.Database.CreateContext();
        await using var second = _world.Database.CreateContext();

        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        first.CourseSessions.Add(_world.NewSession(Room1, Teacher1, Slot));
        await first.SaveChangesAsync();

        second.CourseSessions.Add(_world.NewSession(Room1, Teacher2, Slot));
        var secondInsert = second.SaveChangesAsync();
        await Task.Delay(300);

        await firstTransaction.RollbackAsync();

        await secondInsert;
        Assert.Single(await _world.SessionsAsync());
    }
}
