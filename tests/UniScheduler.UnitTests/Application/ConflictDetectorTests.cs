using FluentAssertions;
using UniScheduler.Application.Common.Models;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;
using UniScheduler.Infrastructure.Scheduler;
using Xunit;

namespace UniScheduler.UnitTests.Application;

public class ConflictDetectorTests
{
    private readonly ConflictDetector _sut = new();
    private static readonly Guid ScheduleId = Guid.NewGuid();

    private static ScheduleEntry MakeEntry(
        Guid? teacherId = null,
        Guid? roomId = null,
        RussianDayOfWeek day = RussianDayOfWeek.Monday,
        int pair = 1,
        WeekType weekType = WeekType.Both,
        bool isOnline = false,
        IEnumerable<Guid>? groupIds = null)
    {
        var entry = new ScheduleEntry
        {
            ScheduleId = ScheduleId,
            TeacherId = teacherId ?? Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            RoomId = roomId,
            DayOfWeek = day,
            PairNumber = pair,
            WeekType = weekType,
            IsOnline = isOnline
        };
        foreach (var gid in groupIds ?? [])
            entry.StudentGroups.Add(new ScheduleEntryStudentGroup { StudentGroupId = gid });
        return entry;
    }

    [Fact]
    public void NoConflicts_WhenDifferentDay()
    {
        var existing = MakeEntry(day: RussianDayOfWeek.Tuesday, pair: 1);
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, Guid.NewGuid(), Guid.NewGuid(), [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, false, [existing]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void NoConflicts_WhenDifferentPair()
    {
        var existing = MakeEntry(day: RussianDayOfWeek.Monday, pair: 2);
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, Guid.NewGuid(), Guid.NewGuid(), [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, false, [existing]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void RoomConflict_WhenSameRoomSameSlot()
    {
        var roomId = Guid.NewGuid();
        var existing = MakeEntry(roomId: roomId, day: RussianDayOfWeek.Monday, pair: 1);
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, roomId, Guid.NewGuid(), [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, false, [existing]);
        result.Should().ContainSingle(c => c.Type == ConflictType.RoomDoubleBooked);
    }

    [Fact]
    public void NoRoomConflict_WhenEntryIsOnline()
    {
        var roomId = Guid.NewGuid();
        var existing = MakeEntry(roomId: roomId, day: RussianDayOfWeek.Monday, pair: 1);
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, roomId, Guid.NewGuid(), [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, isOnline: true, [existing]);
        result.Should().NotContain(c => c.Type == ConflictType.RoomDoubleBooked);
    }

    [Fact]
    public void NoRoomConflict_WhenNoRoomId()
    {
        var existing = MakeEntry(roomId: Guid.NewGuid(), day: RussianDayOfWeek.Monday, pair: 1);
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, null, Guid.NewGuid(), [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, false, [existing]);
        result.Should().NotContain(c => c.Type == ConflictType.RoomDoubleBooked);
    }

    [Fact]
    public void TeacherConflict_WhenSameTeacherSameSlot()
    {
        var teacherId = Guid.NewGuid();
        var existing = MakeEntry(teacherId: teacherId, day: RussianDayOfWeek.Monday, pair: 1);
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, null, teacherId, [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, false, [existing]);
        result.Should().ContainSingle(c => c.Type == ConflictType.TeacherDoubleBooked);
    }

    [Fact]
    public void GroupConflict_WhenSharedGroupSameSlot()
    {
        var groupId = Guid.NewGuid();
        var existing = MakeEntry(day: RussianDayOfWeek.Monday, pair: 1, groupIds: [groupId]);
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, null, Guid.NewGuid(), [groupId],
            RussianDayOfWeek.Monday, 1, WeekType.Both, false, [existing]);
        result.Should().ContainSingle(c => c.Type == ConflictType.GroupDoubleBooked);
    }

    [Fact]
    public void NoGroupConflict_WhenDifferentGroups()
    {
        var existing = MakeEntry(day: RussianDayOfWeek.Monday, pair: 1, groupIds: [Guid.NewGuid()]);
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, null, Guid.NewGuid(), [Guid.NewGuid()],
            RussianDayOfWeek.Monday, 1, WeekType.Both, false, [existing]);
        result.Should().NotContain(c => c.Type == ConflictType.GroupDoubleBooked);
    }

    [Theory]
    [InlineData(WeekType.Both, WeekType.Odd, true)]
    [InlineData(WeekType.Both, WeekType.Even, true)]
    [InlineData(WeekType.Both, WeekType.Both, true)]
    [InlineData(WeekType.Odd, WeekType.Even, false)]
    [InlineData(WeekType.Even, WeekType.Odd, false)]
    [InlineData(WeekType.Odd, WeekType.Odd, true)]
    [InlineData(WeekType.Even, WeekType.Even, true)]
    public void WeekTypeOverlap_ReturnsExpected(WeekType existing, WeekType incoming, bool shouldConflict)
    {
        var teacherId = Guid.NewGuid();
        var entry = MakeEntry(teacherId: teacherId, day: RussianDayOfWeek.Monday, pair: 1, weekType: existing);
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, null, teacherId, [],
            RussianDayOfWeek.Monday, 1, incoming, false, [entry]);
        if (shouldConflict)
            result.Should().ContainSingle(c => c.Type == ConflictType.TeacherDoubleBooked);
        else
            result.Should().BeEmpty();
    }

    [Fact]
    public void SelfEntry_IsSkipped()
    {
        var teacherId = Guid.NewGuid();
        var entry = MakeEntry(teacherId: teacherId, day: RussianDayOfWeek.Monday, pair: 1);
        // Passing the same entry's own Id - it must be skipped
        var result = _sut.DetectConflicts(
            entry.Id, ScheduleId, null, teacherId, [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, false, [entry]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void MultipleConflicts_AllDetected()
    {
        var teacherId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var existing = MakeEntry(teacherId: teacherId, roomId: roomId,
            day: RussianDayOfWeek.Monday, pair: 1, groupIds: [groupId]);
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, roomId, teacherId, [groupId],
            RussianDayOfWeek.Monday, 1, WeekType.Both, false, [existing]);
        result.Should().HaveCount(3);
    }

    [Fact]
    public void EmptyExistingEntries_ProducesNoConflicts()
    {
        var result = _sut.DetectConflicts(
            Guid.Empty, ScheduleId, Guid.NewGuid(), Guid.NewGuid(), [],
            RussianDayOfWeek.Monday, 1, WeekType.Both, false, []);
        result.Should().BeEmpty();
    }
}
