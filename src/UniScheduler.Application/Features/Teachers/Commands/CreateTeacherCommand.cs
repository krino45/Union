using MediatR;
using UniScheduler.Application.Common.Interfaces;
using UniScheduler.Application.DTOs;
using UniScheduler.Domain.Entities;

namespace UniScheduler.Application.Features.Teachers.Commands;

public record CreateTeacherCommand(string FirstName, string LastName, string MiddleName, string Email) : IRequest<TeacherDto>;

public class CreateTeacherCommandHandler : IRequestHandler<CreateTeacherCommand, TeacherDto>
{
    private readonly IApplicationDbContext db;

    public CreateTeacherCommandHandler(IApplicationDbContext db) => this.db = db;

    public async Task<TeacherDto> Handle(CreateTeacherCommand r, CancellationToken cancellationToken)
    {
        var teacher = new Teacher
        {
            FirstName = r.FirstName, LastName = r.LastName,
            MiddleName = r.MiddleName, Email = r.Email
        };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync(cancellationToken);
        return new TeacherDto(teacher.Id, teacher.FirstName, teacher.LastName, teacher.MiddleName, teacher.DisplayName, teacher.Email, new());
    }
}
