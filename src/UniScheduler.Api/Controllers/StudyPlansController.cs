using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniScheduler.Application.DTOs;
using UniScheduler.Application.Features.StudyPlans;
using UniScheduler.Domain.Enums;

namespace UniScheduler.Api.Controllers;

[ApiController]
[Route("api/study-plans")]
[Authorize(Roles = "Admin")]
public class StudyPlansController : ControllerBase
{
    private readonly IMediator _mediator;
    public StudyPlansController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<List<StudyPlanDto>>> GetAll(
        [FromQuery] int? academicYear, [FromQuery] Term? term, CancellationToken ct)
        => Ok(await _mediator.Send(new GetStudyPlansQuery(academicYear, term), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StudyPlanDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetStudyPlanQuery(id), ct));

    [HttpPost]
    public async Task<ActionResult<StudyPlanDto>> Create([FromBody] UpsertStudyPlanDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateStudyPlanCommand(dto), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StudyPlanDto>> Update(Guid id, [FromBody] UpsertStudyPlanDto dto, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateStudyPlanCommand(id, dto), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteStudyPlanCommand(id), ct);
        return NoContent();
    }
}
