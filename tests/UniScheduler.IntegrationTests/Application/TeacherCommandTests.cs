using FluentAssertions;
using UniScheduler.Application.Common.Exceptions;
using UniScheduler.Application.Features.Teachers.Commands;
using UniScheduler.Domain.Entities;
using UniScheduler.Domain.Enums;
using UniScheduler.IntegrationTests.Helpers;
using Xunit;

namespace UniScheduler.IntegrationTests.Application;

public class TeacherCommandTests
{
    [Fact]
    public async Task CreateTeacher_PersistsCorrectFields()
    {
        using var db = DbContextFactory.Create();
        var result = await new CreateTeacherCommandHandler(db)
            .Handle(new CreateTeacherCommand("Иван", "Иванов", "Иванович", "ivan@uni.ru"),
                CancellationToken.None);

        result.FirstName.Should().Be("Иван");
        result.LastName.Should().Be("Иванов");
        result.Email.Should().Be("ivan@uni.ru");
        result.DisplayName.Should().Be("Иванов Иван Иванович");
        db.Teachers.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteTeacher_RemovesRecord()
    {
        using var db = DbContextFactory.Create();
        var teacher = new Teacher { FirstName = "Иван", LastName = "Иванов", MiddleName = "", Email = "i@uni.ru" };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();

        await new DeleteTeacherCommandHandler(db)
            .Handle(new DeleteTeacherCommand(teacher.Id), CancellationToken.None);

        db.Teachers.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteTeacher_Missing_ThrowsNotFoundException()
    {
        using var db = DbContextFactory.Create();
        var act = async () => await new DeleteTeacherCommandHandler(db)
            .Handle(new DeleteTeacherCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetTeacherSubjects_ReplacesExisting()
    {
        using var db = DbContextFactory.Create();
        var teacher = new Teacher { FirstName = "А", LastName = "Б", MiddleName = "", Email = "ab@uni.ru" };
        var subjectOld = new Subject { Name = "Старый", ShortName = "СТ", AcademicYear = 2026, Term = Term.First };
        var subjectNew = new Subject { Name = "Новый", ShortName = "НВ", AcademicYear = 2026, Term = Term.First };
        db.Teachers.Add(teacher);
        db.Subjects.AddRange(subjectOld, subjectNew);
        await db.SaveChangesAsync();

        db.TeacherSubjects.Add(new TeacherSubject
        {
            TeacherId = teacher.Id, SubjectId = subjectOld.Id, LessonType = LessonType.Lecture
        });
        await db.SaveChangesAsync();

        await new SetTeacherSubjectsCommandHandler(db)
            .Handle(new SetTeacherSubjectsCommand(teacher.Id,
                [new TeacherSubjectAssignment(subjectNew.Id, LessonType.Practical, null)]),
                CancellationToken.None);

        db.TeacherSubjects.Should().ContainSingle(ts => ts.SubjectId == subjectNew.Id);
        db.TeacherSubjects.Should().NotContain(ts => ts.SubjectId == subjectOld.Id);
    }

    [Fact]
    public async Task SetTeacherSubjects_ClearsAll_WhenEmptyList()
    {
        using var db = DbContextFactory.Create();
        var teacher = new Teacher { FirstName = "А", LastName = "Б", MiddleName = "", Email = "ab@uni.ru" };
        var subject = new Subject { Name = "X", ShortName = "X", AcademicYear = 2026, Term = Term.First };
        db.Teachers.Add(teacher);
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        db.TeacherSubjects.Add(new TeacherSubject
        {
            TeacherId = teacher.Id, SubjectId = subject.Id, LessonType = LessonType.Lecture
        });
        await db.SaveChangesAsync();

        await new SetTeacherSubjectsCommandHandler(db)
            .Handle(new SetTeacherSubjectsCommand(teacher.Id, []), CancellationToken.None);

        db.TeacherSubjects.Should().BeEmpty();
    }

    [Fact]
    public async Task SetTeacherSubjects_Missing_ThrowsNotFoundException()
    {
        using var db = DbContextFactory.Create();
        var act = async () => await new SetTeacherSubjectsCommandHandler(db)
            .Handle(new SetTeacherSubjectsCommand(Guid.NewGuid(), []), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
